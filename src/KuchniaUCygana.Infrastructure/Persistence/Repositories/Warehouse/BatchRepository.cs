using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public class BatchRepository : BaseRepository<Batch>, IBatchRepository
{
    public BatchRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Batch>> GetActiveBatchesByStockItemAsync(int stockItemId)
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Batch>(
            """
            SELECT *
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDepleted] = 0
              AND [IsDeleted] = 0
            ORDER BY
              CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
              [ExpiryDate];
            """,
            new { stockItemId });
    }

    public async Task<IEnumerable<Batch>> GetBatchesByStockItemAsync(int stockItemId)
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Batch>(
            """
            SELECT *
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0
            ORDER BY
              CASE WHEN [IsDepleted] = 0 THEN 0 ELSE 1 END,
              CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
              [ExpiryDate],
              [Id];
            """,
            new { stockItemId });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Batch>> GetExpiringBeforeAsync(DateTimeOffset date)
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Batch>(
            """
            SELECT *
            FROM [Batches]
            WHERE [ExpiryDate] IS NOT NULL
              AND [ExpiryDate] <= @date
              AND [IsDepleted] = 0
              AND [IsDeleted] = 0;
            """,
            new { date });
    }

    public async Task<(IEnumerable<FefoReportRow> Items, int TotalCount)> GetFefoReportPageAsync(FefoReportQuery query)
    {
        using var db = Factory.CreateConnection();

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;
        var parameters = CreateFefoParameters(query, offset, pageSize);

        var totalCount = await db.ExecuteScalarAsync<int>($"{FefoRowsCte} {FefoRowsCountSql}", parameters);
        var items = await db.QueryAsync<FefoReportRow>($"{FefoRowsCte} {FefoRowsPageSql}", parameters);

        return (items, totalCount);
    }

    public async Task<IEnumerable<FefoReportRow>> GetFefoReportAsync(FefoReportQuery query)
    {
        using var db = Factory.CreateConnection();

        var parameters = CreateFefoParameters(query, offset: 0, pageSize: int.MaxValue);
        return await db.QueryAsync<FefoReportRow>($"{FefoRowsCte} {FefoRowsAllSql}", parameters);
    }

    public async Task<(IEnumerable<BatchInventoryRow> Items, int TotalCount)> GetBatchInventoryPageAsync(
        BatchInventoryQuery query)
    {
        using var db = Factory.CreateConnection();

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;
        var search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : $"%{query.Search.Trim()}%";
        var category = string.IsNullOrWhiteSpace(query.Category)
            ? null
            : query.Category.Trim();

        var parameters = new
        {
            search,
            category,
            offset,
            pageSize,
        };

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"{BatchInventoryRowsCte} {BatchInventoryRowsCountSql}",
            parameters);
        var items = await db.QueryAsync<BatchInventoryRow>(
            $"{BatchInventoryRowsCte} {BatchInventoryRowsPageSql}",
            parameters);

        return (items, totalCount);
    }

    private static object CreateFefoParameters(FefoReportQuery query, int offset, int pageSize)
    {
        var search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : $"%{query.Search.Trim()}%";
        var status = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : query.Status.Trim();

        return new
        {
            search,
            status,
            now = DateTimeOffset.UtcNow,
            offset,
            pageSize,
        };
    }

    private const string FefoRowsCte = """
        WITH FefoRows AS (
            SELECT
                b.[StockItemId],
                si.[Name] AS [StockItemName],
                b.[Id] AS [BatchId],
                COALESCE(b.[SupplierBatchNumber], '') AS [BatchNumber],
                b.[ExpiryDate],
                b.[CurrentQuantity] AS [Quantity],
                CASE
                    WHEN b.[ExpiryDate] IS NULL THEN NULL
                    ELSE DATEDIFF(day, @now, b.[ExpiryDate])
                END AS [DaysToExpiry],
                CASE
                    WHEN b.[ExpiryDate] IS NOT NULL AND b.[ExpiryDate] <= @now THEN 'Expired'
                    WHEN b.[ExpiryDate] IS NOT NULL AND DATEDIFF(day, @now, b.[ExpiryDate]) <= 3 THEN 'Critical'
                    WHEN b.[ExpiryDate] IS NOT NULL AND DATEDIFF(day, @now, b.[ExpiryDate]) <= 7 THEN 'Warning'
                    ELSE 'Safe'
                END AS [Status]
            FROM [Batches] b
            INNER JOIN [StockItems] si ON si.[Id] = b.[StockItemId]
            WHERE b.[IsDeleted] = 0
              AND b.[IsDepleted] = 0
              AND si.[IsDeleted] = 0
              AND (@search IS NULL OR si.[Name] LIKE @search OR b.[SupplierBatchNumber] LIKE @search)
        )
        """;

    private const string FefoRowsWhereSql = """
        WHERE (@status IS NULL OR [Status] = @status)
        """;

    private const string FefoRowsCountSql = $"""
        SELECT COUNT(*)
        FROM FefoRows
        {FefoRowsWhereSql};
        """;

    private const string FefoRowsOrderSql = """
        ORDER BY
            CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
            [ExpiryDate] ASC,
            [StockItemName] ASC,
            [BatchId] ASC
        """;

    private const string FefoRowsAllSql = $"""
        SELECT *
        FROM FefoRows
        {FefoRowsWhereSql}
        {FefoRowsOrderSql};
        """;

    private const string FefoRowsPageSql = $"""
        SELECT *
        FROM FefoRows
        {FefoRowsWhereSql}
        {FefoRowsOrderSql}
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    private const string BatchInventoryRowsCte = """
        WITH BatchInventoryRows AS (
            SELECT
                b.[Id] AS [BatchId],
                b.[StockItemId],
                si.[Name] AS [StockItemName],
                COALESCE(b.[SupplierBatchNumber], '') AS [BatchNumber],
                category.[Category],
                b.[CurrentQuantity],
                uom.[Symbol] AS [UnitSymbol],
                b.[ExpiryDate],
                b.[ReceivedDate]
            FROM [Batches] b
            INNER JOIN [StockItems] si ON si.[Id] = b.[StockItemId]
            INNER JOIN [UnitsOfMeasure] uom ON uom.[Id] = si.[DefaultUnitOfMeasureId]
            CROSS APPLY (
                SELECT CASE
                    WHEN LOWER(si.[Name]) LIKE N'%pudełko%'
                      OR LOWER(si.[Name]) LIKE N'%torba%'
                      OR LOWER(si.[Name]) LIKE N'%opakow%' THEN N'Opakowania'
                    WHEN LOWER(si.[Name]) LIKE N'%kurczak%'
                      OR LOWER(si.[Name]) LIKE N'%łosoś%'
                      OR LOWER(si.[Name]) LIKE N'%mięso%'
                      OR LOWER(si.[Name]) LIKE N'%ryb%'
                      OR LOWER(si.[Name]) LIKE N'%indyka%' THEN N'Mięso/Ryby'
                    WHEN LOWER(si.[Name]) LIKE N'%śmietanka%'
                      OR LOWER(si.[Name]) LIKE N'%masło%'
                      OR LOWER(si.[Name]) LIKE N'%ser %'
                      OR LOWER(si.[Name]) LIKE N'%gouda%'
                      OR LOWER(si.[Name]) LIKE N'%jogurt%'
                      OR LOWER(si.[Name]) LIKE N'%mleko%' THEN N'Nabiał'
                    WHEN LOWER(si.[Name]) LIKE N'%brokuł%'
                      OR LOWER(si.[Name]) LIKE N'%dynia%'
                      OR LOWER(si.[Name]) LIKE N'%batat%'
                      OR LOWER(si.[Name]) LIKE N'%jagod%'
                      OR LOWER(si.[Name]) LIKE N'%ziemniak%'
                      OR (LOWER(si.[Name]) LIKE N'%pomidor%' AND LOWER(si.[Name]) NOT LIKE N'%puszka%') THEN N'Warzywa i owoce'
                    ELSE N'Suche'
                END AS [Category]
            ) category
            WHERE b.[IsDeleted] = 0
              AND b.[IsDepleted] = 0
              AND b.[CurrentQuantity] > 0
              AND si.[IsDeleted] = 0
              AND (@search IS NULL OR si.[Name] LIKE @search OR b.[SupplierBatchNumber] LIKE @search)
        )
        """;

    private const string BatchInventoryRowsWhereSql = """
        WHERE (@category IS NULL OR [Category] = @category)
        """;

    private const string BatchInventoryRowsCountSql = $"""
        SELECT COUNT(*)
        FROM BatchInventoryRows
        {BatchInventoryRowsWhereSql};
        """;

    private const string BatchInventoryRowsOrderSql = """
        ORDER BY
            [StockItemName] ASC,
            CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
            [ExpiryDate] ASC,
            [BatchId] ASC
        """;

    private const string BatchInventoryRowsPageSql = $"""
        SELECT *
        FROM BatchInventoryRows
        {BatchInventoryRowsWhereSql}
        {BatchInventoryRowsOrderSql}
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;
}
