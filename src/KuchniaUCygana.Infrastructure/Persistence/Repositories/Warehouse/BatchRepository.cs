using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public class BatchRepository : BaseRepository<Batch>, IBatchRepository
{
    public BatchRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

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
              [ExpiryDate],
              [Id];
            """,
            new { stockItemId });
    }

    public async Task<IEnumerable<Batch>> GetActiveBatchesByWarehouseCategoryAsync(int warehouseCategoryId)
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Batch>(
            """
            SELECT b.*
            FROM [Batches] b
            INNER JOIN [StockItems] si ON si.[Id] = b.[StockItemId]
            WHERE si.[WarehouseCategoryId] = @warehouseCategoryId
              AND si.[IsDeleted] = 0
              AND b.[IsDepleted] = 0
              AND b.[IsDeleted] = 0
            ORDER BY
              CASE WHEN b.[ExpiryDate] IS NULL THEN 1 ELSE 0 END,
              b.[ExpiryDate],
              b.[Id];
            """,
            new { warehouseCategoryId });
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
        var search = CreateSearchParameters(query.Search);
        var parameters = CreateFefoParameters(query, offset, pageSize, search);
        var sql = CreateFefoRowsPagedQuerySql(search.UseContains);

        using var multi = await db.QueryMultipleAsync(sql, parameters);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<FefoReportRow>()).ToList();

        return (items, totalCount);
    }

    public async Task<IEnumerable<FefoReportRow>> GetFefoReportAsync(FefoReportQuery query)
    {
        using var db = Factory.CreateConnection();

        var search = CreateSearchParameters(query.Search);
        var parameters = CreateFefoParameters(query, offset: 0, pageSize: int.MaxValue, search);
        var sql = CreateFefoRowsAllQuerySql(search.UseContains);
        return await db.QueryAsync<FefoReportRow>(sql, parameters);
    }

    public async Task<(IEnumerable<BatchInventoryRow> Items, int TotalCount)> GetBatchInventoryPageAsync(
        BatchInventoryQuery query)
    {
        using var db = Factory.CreateConnection();

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;
        var search = CreateSearchParameters(query.Search);
        var legacyCategory = string.IsNullOrWhiteSpace(query.LegacyCategory)
            ? null
            : query.LegacyCategory.Trim();

        var parameters = new
        {
            searchExact = search.Exact,
            searchPrefix = search.Prefix,
            searchContains = search.Contains,
            categoryId = query.CategoryId,
            legacyCategory,
            offset,
            pageSize,
        };

        var sql = CreateBatchInventoryRowsPagedQuerySql(search.UseContains);
        using var multi = await db.QueryMultipleAsync(sql, parameters);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<BatchInventoryRow>()).ToList();

        return (items, totalCount);
    }

    private static object CreateFefoParameters(
        FefoReportQuery query,
        int offset,
        int pageSize,
        SearchParameters search)
    {
        var status = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : query.Status.Trim();

        return new
        {
            searchExact = search.Exact,
            searchPrefix = search.Prefix,
            searchContains = search.Contains,
            status,
            now = DateTimeOffset.UtcNow,
            offset,
            pageSize,
        };
    }

    private static SearchParameters CreateSearchParameters(string? value)
    {
        var term = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return new SearchParameters(
            term,
            term is null ? null : $"{term}%",
            term is { Length: >= 3 } ? $"%{term}%" : null,
            term is { Length: >= 3 });
    }

    private static string CreateFefoRowsPagedQuerySql(bool includeContains)
    {
        var searchPredicate = includeContains
            ? BatchSearchPredicateContainsSql
            : BatchSearchPredicatePrefixSql;

        return $"""
            {CreateFefoRowsTempTableSql(searchPredicate)}
            {FefoRowsCountSql}
            {FefoRowsPageSql}
            DROP TABLE #FefoRows;
            """;
    }

    private static string CreateFefoRowsAllQuerySql(bool includeContains)
    {
        var searchPredicate = includeContains
            ? BatchSearchPredicateContainsSql
            : BatchSearchPredicatePrefixSql;

        return $"""
            {CreateFefoRowsTempTableSql(searchPredicate)}
            {FefoRowsAllSql}
            DROP TABLE #FefoRows;
            """;
    }

    private static string CreateBatchInventoryRowsPagedQuerySql(bool includeContains)
    {
        var searchPredicate = includeContains
            ? BatchSearchPredicateContainsSql
            : BatchSearchPredicatePrefixSql;

        return $"""
            {CreateBatchInventoryRowsTempTableSql(searchPredicate)}
            {BatchInventoryRowsCountSql}
            {BatchInventoryRowsPageSql}
            DROP TABLE #BatchInventoryRows;
            """;
    }

    private static string CreateFefoRowsTempTableSql(string searchPredicate)
    {
        return $"""
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
                  {searchPredicate}
            )
            SELECT *
            INTO #FefoRows
            FROM FefoRows;
            """;
    }

    private const string FefoRowsWhereSql = """
        WHERE (@status IS NULL OR [Status] = @status)
        """;

    private const string FefoRowsCountSql = $"""
        SELECT COUNT(*)
        FROM #FefoRows
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
        FROM #FefoRows
        {FefoRowsWhereSql}
        {FefoRowsOrderSql};
        """;

    private const string FefoRowsPageSql = $"""
        SELECT *
        FROM #FefoRows
        {FefoRowsWhereSql}
        {FefoRowsOrderSql}
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    private static string CreateBatchInventoryRowsTempTableSql(string searchPredicate)
    {
        return $"""
            WITH BatchInventoryRows AS (
                SELECT
                    b.[Id] AS [BatchId],
                    b.[StockItemId],
                    si.[Name] AS [StockItemName],
                    COALESCE(b.[SupplierBatchNumber], '') AS [BatchNumber],
                    si.[WarehouseCategoryId] AS [CategoryId],
                    wc.[Name] AS [Category],
                    b.[CurrentQuantity],
                    uom.[Symbol] AS [UnitSymbol],
                    b.[ExpiryDate],
                    b.[ReceivedDate]
                FROM [Batches] b
                INNER JOIN [StockItems] si ON si.[Id] = b.[StockItemId]
                INNER JOIN [WarehouseCategories] wc ON wc.[Id] = si.[WarehouseCategoryId]
                INNER JOIN [UnitsOfMeasure] uom ON uom.[Id] = si.[DefaultUnitOfMeasureId]
                WHERE b.[IsDeleted] = 0
                  AND b.[IsDepleted] = 0
                  AND b.[CurrentQuantity] > 0
                  AND si.[IsDeleted] = 0
                  AND (@categoryId IS NULL OR si.[WarehouseCategoryId] = @categoryId)
                  AND (@legacyCategory IS NULL OR @categoryId IS NOT NULL OR wc.[Name] = @legacyCategory)
                  {searchPredicate}
            )
            SELECT *
            INTO #BatchInventoryRows
            FROM BatchInventoryRows;
            """;
    }

    private const string BatchSearchPredicatePrefixSql = """
        AND (
                @searchExact IS NULL
                OR si.[Name] = @searchExact
                OR si.[Name] LIKE @searchPrefix
                OR b.[SupplierBatchNumber] = @searchExact
                OR b.[SupplierBatchNumber] LIKE @searchPrefix
        )
        """;

    private const string BatchSearchPredicateContainsSql = """
        AND (
                @searchExact IS NULL
                OR si.[Name] = @searchExact
                OR si.[Name] LIKE @searchPrefix
                OR si.[Name] LIKE @searchContains
                OR b.[SupplierBatchNumber] = @searchExact
                OR b.[SupplierBatchNumber] LIKE @searchPrefix
                OR b.[SupplierBatchNumber] LIKE @searchContains
        )
        """;

    private const string BatchInventoryRowsCountSql = """
        SELECT COUNT(*)
        FROM #BatchInventoryRows;
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
        FROM #BatchInventoryRows
        {BatchInventoryRowsOrderSql}
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    private sealed record SearchParameters(
        string? Exact,
        string? Prefix,
        string? Contains,
        bool UseContains);
}


