using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class StockItemRepository : BaseRepository<StockItem>, IStockItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockItemRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<StockItem>> GetBelowMinimumAsync()
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QueryAsync<StockItem>(
            """
            SELECT si.*
            FROM [StockItems] si
            OUTER APPLY (
                SELECT COALESCE(SUM(b.[CurrentQuantity]), 0) AS TotalQuantity
                FROM [Batches] b
                WHERE b.[StockItemId] = si.[Id]
                  AND b.[IsDepleted] = 0
                  AND b.[IsDeleted] = 0
            ) totals
            WHERE si.[IsDeleted] = 0
              AND si.[MinimumLevel] > 0
              AND totals.TotalQuantity < si.[MinimumLevel];
            """);
    }

    public async Task<StockItem?> GetByIngredientIdAsync(int baseIngredientId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<StockItem>(
            """
            SELECT TOP 1 *
            FROM [StockItems]
            WHERE [BaseIngredientId] = @baseIngredientId
              AND [IsDeleted] = 0;
            """,
            new { baseIngredientId });
    }

    public async Task<StockItem?> GetWithBatchesAsync(int stockItemId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<StockItem>(
            """
            SELECT *
            FROM [StockItems]
            WHERE [Id] = @stockItemId
              AND [IsDeleted] = 0;
            """,
            new { stockItemId });
    }

    public async Task<(IEnumerable<StockItem> Items, int TotalCount)> GetPagedAsync(StockItemFilter filter)
    {
        using var db = _connectionFactory.CreateConnection();

        var searchParam = string.IsNullOrWhiteSpace(filter.SearchTerm)
            ? null
            : $"%{filter.SearchTerm.Trim()}%";
        var offset = (filter.Page - 1) * filter.PageSize;

        const string countSql = """
            SELECT COUNT(DISTINCT si.[Id])
            FROM [StockItems] si
            LEFT JOIN [Batches] b ON b.[StockItemId] = si.[Id] AND b.[IsDeleted] = 0 AND b.[IsDepleted] = 0
            WHERE si.[IsDeleted] = 0
              AND (@search IS NULL OR si.[Name] LIKE @search OR b.[SupplierBatchNumber] LIKE @search);
            """;

        const string itemsSql = """
            SELECT DISTINCT si.*
            FROM [StockItems] si
            LEFT JOIN [Batches] b ON b.[StockItemId] = si.[Id] AND b.[IsDeleted] = 0 AND b.[IsDepleted] = 0
            WHERE si.[IsDeleted] = 0
              AND (@search IS NULL OR si.[Name] LIKE @search OR b.[SupplierBatchNumber] LIKE @search)
            ORDER BY si.[Name] ASC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        var parameters = new { search = searchParam, offset, pageSize = filter.PageSize };

        var totalCount = await db.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await db.QueryAsync<StockItem>(itemsSql, parameters);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<StockItemStockRow> Items, int TotalCount)> GetStockTablePageAsync(
        StockItemTableQuery query)
    {
        using var db = _connectionFactory.CreateConnection();

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;
        var search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : $"%{query.Search.Trim()}%";
        var legacyCategory = string.IsNullOrWhiteSpace(query.LegacyCategory)
            ? null
            : query.LegacyCategory.Trim();
        var now = DateTimeOffset.UtcNow;
        var soonCutoff = now.AddDays(7);

        var parameters = new
        {
            search,
            categoryId = query.CategoryId,
            legacyCategory,
            showExpiredOnly = query.ShowExpiredOnly,
            showLowStockOnly = query.ShowLowStockOnly,
            showExpiringSoonOnly = query.ShowExpiringSoonOnly,
            now,
            soonCutoff,
            offset,
            pageSize,
        };

        var totalCount = await db.ExecuteScalarAsync<int>($"{StockRowsCte} {StockRowsFilterCountSql}", parameters);
        var items = await db.QueryAsync<StockItemStockRow>($"{StockRowsCte} {StockRowsPageSql}", parameters);

        return (items, totalCount);
    }

    public async Task<IEnumerable<StockItemStockRow>> SearchStockLookupAsync(
        string query,
        int limit,
        bool onlyAvailable)
    {
        using var db = _connectionFactory.CreateConnection();

        var search = $"%{query.Trim()}%";
        var safeLimit = Math.Clamp(limit, 1, 50);

        return await db.QueryAsync<StockItemStockRow>(
            $"""
            {StockRowsCte}
            SELECT TOP (@limit)
                [Id],
                [Name],
                [BaseIngredientId],
                [DefaultUnitOfMeasureId],
                [MinimumLevel],
                [LeadTimeDays],
                [CurrentStock],
                [CategoryId],
                [Category],
                [UnitSymbol],
                [EarliestExpiryDate]
            FROM StockRows
            WHERE ([Name] LIKE @search OR EXISTS (
                    SELECT 1
                    FROM [Batches] b
                    WHERE b.[StockItemId] = StockRows.[Id]
                      AND b.[IsDeleted] = 0
                      AND b.[IsDepleted] = 0
                      AND b.[SupplierBatchNumber] LIKE @search
                ))
              AND (@onlyAvailable = 0 OR [CurrentStock] > 0)
            ORDER BY
                CASE WHEN [Name] LIKE @prefix THEN 0 ELSE 1 END,
                [Name] ASC;
            """,
            new
            {
                search,
                prefix = $"{query.Trim()}%",
                limit = safeLimit,
                onlyAvailable,
            });
    }

    public async Task<StockItemStockRow?> GetStockLookupByIdAsync(int stockItemId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<StockItemStockRow>(
            $"""
            {StockRowsCte}
            SELECT TOP 1
                [Id],
                [Name],
                [BaseIngredientId],
                [DefaultUnitOfMeasureId],
                [MinimumLevel],
                [LeadTimeDays],
                [CurrentStock],
                [CategoryId],
                [Category],
                [UnitSymbol],
                [EarliestExpiryDate]
            FROM StockRows
            WHERE [Id] = @stockItemId;
            """,
            new { stockItemId, search = (string?)null });
    }

    public async Task<IEnumerable<SmartInventoryAlertRow>> GetSmartInventoryAlertRowsAsync(DateTimeOffset now)
    {
        using var db = _connectionFactory.CreateConnection();

        var nowUtc = now.UtcDateTime;
        var cutoffUtc = now.UtcDateTime.AddDays(7);

        return await db.QueryAsync<SmartInventoryAlertRow>(
            """
            WITH ActiveBatches AS (
                SELECT
                    b.[Id],
                    b.[StockItemId],
                    b.[SupplierBatchNumber],
                    b.[CurrentQuantity],
                    b.[ExpiryDate]
                FROM [Batches] b
                WHERE b.[IsDeleted] = 0
                  AND b.[IsDepleted] = 0
                  AND b.[CurrentQuantity] > 0
            ),
            StockTotals AS (
                SELECT
                    b.[StockItemId],
                    SUM(b.[CurrentQuantity]) AS [CurrentQuantity]
                FROM ActiveBatches b
                GROUP BY b.[StockItemId]
            )
            SELECT
                si.[Id] AS [StockItemId],
                si.[Name] AS [StockItemName],
                CAST(NULL AS nvarchar(50)) AS [SupplierBatchNumber],
                si.[WarehouseCategoryId] AS [CategoryId],
                wc.[Name] AS [Category],
                CASE WHEN COALESCE(t.[CurrentQuantity], 0) <= 0 THEN N'NoStock' ELSE N'BelowMinimum' END AS [AlertCode],
                COALESCE(t.[CurrentQuantity], 0) AS [CurrentQuantity],
                si.[MinimumLevel],
                CAST(NULL AS datetimeoffset) AS [EarliestExpiry],
                CAST(NULL AS int) AS [DaysUntilExpiry]
            FROM [StockItems] si
            INNER JOIN [WarehouseCategories] wc ON wc.[Id] = si.[WarehouseCategoryId]
            LEFT JOIN StockTotals t ON t.[StockItemId] = si.[Id]
            WHERE si.[IsDeleted] = 0
              AND si.[MinimumLevel] > 0
              AND COALESCE(t.[CurrentQuantity], 0) < si.[MinimumLevel]

            UNION ALL

            SELECT
                si.[Id] AS [StockItemId],
                si.[Name] AS [StockItemName],
                b.[SupplierBatchNumber],
                si.[WarehouseCategoryId] AS [CategoryId],
                wc.[Name] AS [Category],
                CASE
                    WHEN b.[ExpiryDate] <= @nowUtc THEN N'Expired'
                    WHEN DATEDIFF(day, @nowUtc, b.[ExpiryDate]) <= 3 THEN N'ExpiringWithin3Days'
                    ELSE N'ExpiringWithin7Days'
                END AS [AlertCode],
                b.[CurrentQuantity],
                CAST(NULL AS decimal(18, 2)) AS [MinimumLevel],
                b.[ExpiryDate] AS [EarliestExpiry],
                DATEDIFF(day, @nowUtc, b.[ExpiryDate]) AS [DaysUntilExpiry]
            FROM ActiveBatches b
            INNER JOIN [StockItems] si ON si.[Id] = b.[StockItemId] AND si.[IsDeleted] = 0
            INNER JOIN [WarehouseCategories] wc ON wc.[Id] = si.[WarehouseCategoryId]
            WHERE b.[ExpiryDate] IS NOT NULL
              AND b.[ExpiryDate] <= @cutoffUtc
            ORDER BY [AlertCode], [DaysUntilExpiry], [StockItemName];
            """,
            new { nowUtc, cutoffUtc });
    }

    private const string StockRowsCte = """
        WITH StockRows AS (
            SELECT
                si.[Id],
                si.[Name],
                si.[BaseIngredientId],
                si.[DefaultUnitOfMeasureId],
                si.[MinimumLevel],
                si.[LeadTimeDays],
                COALESCE(totals.[CurrentStock], 0) AS [CurrentStock],
                si.[WarehouseCategoryId] AS [CategoryId],
                wc.[Name] AS [Category],
                uom.[Symbol] AS [UnitSymbol],
                totals.[EarliestExpiryDate]
            FROM [StockItems] si
            INNER JOIN [WarehouseCategories] wc ON wc.[Id] = si.[WarehouseCategoryId]
            INNER JOIN [UnitsOfMeasure] uom ON uom.[Id] = si.[DefaultUnitOfMeasureId]
            OUTER APPLY (
                SELECT
                    COALESCE(SUM(b.[CurrentQuantity]), 0) AS [CurrentStock],
                    MIN(b.[ExpiryDate]) AS [EarliestExpiryDate]
                FROM [Batches] b
                WHERE b.[StockItemId] = si.[Id]
                  AND b.[IsDeleted] = 0
                  AND b.[IsDepleted] = 0
            ) totals
            WHERE si.[IsDeleted] = 0
              AND (@search IS NULL OR si.[Name] LIKE @search OR EXISTS (
                    SELECT 1
                    FROM [Batches] searchBatches
                    WHERE searchBatches.[StockItemId] = si.[Id]
                      AND searchBatches.[IsDeleted] = 0
                      AND searchBatches.[IsDepleted] = 0
                      AND searchBatches.[SupplierBatchNumber] LIKE @search
                ))
        )
        """;

    private const string StockRowsWhereSql = """
        WHERE (@categoryId IS NULL OR [CategoryId] = @categoryId)
          AND (@legacyCategory IS NULL OR @categoryId IS NOT NULL OR [Category] = @legacyCategory)
          AND (@showLowStockOnly = 0 OR [CurrentStock] < [MinimumLevel])
          AND (
                (@showExpiredOnly = 0 AND @showExpiringSoonOnly = 0)
                OR (@showExpiredOnly = 1 AND [EarliestExpiryDate] <= @now)
                OR (@showExpiringSoonOnly = 1 AND [EarliestExpiryDate] > @now AND [EarliestExpiryDate] <= @soonCutoff)
          )
        """;

    private const string StockRowsFilterCountSql = $"""
        SELECT COUNT(*)
        FROM StockRows
        {StockRowsWhereSql};
        """;

    private const string StockRowsPageSql = $"""
        SELECT
            [Id],
            [Name],
            [BaseIngredientId],
            [DefaultUnitOfMeasureId],
            [MinimumLevel],
            [LeadTimeDays],
            [CurrentStock],
            [CategoryId],
            [Category],
            [UnitSymbol],
            [EarliestExpiryDate]
        FROM StockRows
        {StockRowsWhereSql}
        ORDER BY [Name] ASC, [Id] ASC
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;
}
