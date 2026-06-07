using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class StockItemRepository : BaseRepository<StockItem>, IStockItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockItemRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<StockItem>> GetBelowMinimumAsync()
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QueryAsync<StockItem>(
            """
            WITH ActiveBatchTotals AS (
                SELECT
                    b.[StockItemId],
                    SUM(b.[CurrentQuantity]) AS [TotalQuantity]
                FROM [Batches] b
                WHERE b.[IsDepleted] = 0
                  AND b.[IsDeleted] = 0
                GROUP BY b.[StockItemId]
            )
            SELECT si.*
            FROM [StockItems] si
            LEFT JOIN ActiveBatchTotals totals ON totals.[StockItemId] = si.[Id]
            WHERE si.[IsDeleted] = 0
              AND si.[MinimumLevel] > 0
              AND COALESCE(totals.[TotalQuantity], 0) < si.[MinimumLevel];
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
        var search = CreateSearchParameters(query.Search);
        var legacyCategory = string.IsNullOrWhiteSpace(query.LegacyCategory)
            ? null
            : query.LegacyCategory.Trim();
        var now = DateTimeOffset.UtcNow;
        var soonCutoff = now.AddDays(7);

        var parameters = new
        {
            searchExact = search.Exact,
            searchPrefix = search.Prefix,
            searchContains = search.Contains,
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

        var sql = CreateStockRowsPagedQuerySql(search.UseContains);
        using var multi = await db.QueryMultipleAsync(sql, parameters);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<StockItemStockRow>()).ToList();

        return (items, totalCount);
    }

    public async Task<IEnumerable<StockItemStockRow>> SearchStockLookupAsync(
        string query,
        int limit,
        bool onlyAvailable)
    {
        using var db = _connectionFactory.CreateConnection();

        var search = CreateSearchParameters(query);
        var safeLimit = Math.Clamp(limit, 1, 50);

        return await db.QueryAsync<StockItemStockRow>(
            CreateStockLookupQuerySql(search.UseContains),
            new
            {
                searchExact = search.Exact,
                searchPrefix = search.Prefix,
                searchContains = search.Contains,
                limit = safeLimit,
                onlyAvailable,
            });
    }

    public async Task<StockItemStockRow?> GetStockLookupByIdAsync(int stockItemId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<StockItemStockRow>(
            """
            WITH ActiveBatchTotals AS (
                SELECT
                    b.[StockItemId],
                    SUM(b.[CurrentQuantity]) AS [CurrentStock],
                    MIN(b.[ExpiryDate]) AS [EarliestExpiryDate]
                FROM [Batches] b
                WHERE b.[StockItemId] = @stockItemId
                  AND b.[IsDeleted] = 0
                  AND b.[IsDepleted] = 0
                GROUP BY b.[StockItemId]
            )
            SELECT TOP 1
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
            LEFT JOIN ActiveBatchTotals totals ON totals.[StockItemId] = si.[Id]
            WHERE si.[Id] = @stockItemId
              AND si.[IsDeleted] = 0;
            """,
            new { stockItemId });
    }

    public async Task<IEnumerable<SmartInventoryAlertRow>> GetSmartInventoryAlertRowsAsync(DateTimeOffset now, int? limit = null)
    {
        using var db = _connectionFactory.CreateConnection();

        var nowUtc = now.UtcDateTime;
        var cutoffUtc = now.UtcDateTime.AddDays(7);
        var safeLimit = limit.HasValue ? Math.Clamp(limit.Value, 1, 500) : int.MaxValue;

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
            ),
            AlertRows AS (
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
            )
            SELECT
                TOP (@limit)
                [StockItemId],
                [StockItemName],
                [SupplierBatchNumber],
                [CategoryId],
                [Category],
                [AlertCode],
                [CurrentQuantity],
                [MinimumLevel],
                [EarliestExpiry],
                [DaysUntilExpiry]
            FROM AlertRows
            ORDER BY [AlertCode], [DaysUntilExpiry], [StockItemName];
            """,
            new { nowUtc, cutoffUtc, limit = safeLimit });
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

    private static string CreateStockLookupQuerySql(bool includeContains)
    {
        var searchPredicate = includeContains
            ? StockLookupSearchPredicateContainsSql
            : StockLookupSearchPredicatePrefixSql;

        return $"""
            WITH CandidateItems AS (
                SELECT
                    si.[Id],
                    si.[Name],
                    si.[BaseIngredientId],
                    si.[DefaultUnitOfMeasureId],
                    si.[MinimumLevel],
                    si.[LeadTimeDays],
                    si.[WarehouseCategoryId] AS [CategoryId],
                    wc.[Name] AS [Category],
                    uom.[Symbol] AS [UnitSymbol],
                    CASE
                        WHEN si.[Name] = @searchExact THEN 0
                        WHEN si.[Name] LIKE @searchPrefix THEN 1
                        WHEN EXISTS (
                            SELECT 1
                            FROM [Batches] exactBatches
                            WHERE exactBatches.[StockItemId] = si.[Id]
                              AND exactBatches.[IsDeleted] = 0
                              AND exactBatches.[IsDepleted] = 0
                              AND exactBatches.[SupplierBatchNumber] = @searchExact
                        ) THEN 2
                        WHEN EXISTS (
                            SELECT 1
                            FROM [Batches] prefixBatches
                            WHERE prefixBatches.[StockItemId] = si.[Id]
                              AND prefixBatches.[IsDeleted] = 0
                              AND prefixBatches.[IsDepleted] = 0
                              AND prefixBatches.[SupplierBatchNumber] LIKE @searchPrefix
                        ) THEN 3
                        ELSE 4
                    END AS [SearchRank]
                FROM [StockItems] si
                INNER JOIN [WarehouseCategories] wc ON wc.[Id] = si.[WarehouseCategoryId]
                INNER JOIN [UnitsOfMeasure] uom ON uom.[Id] = si.[DefaultUnitOfMeasureId]
                WHERE si.[IsDeleted] = 0
                  {searchPredicate}
            ),
            ActiveBatchTotals AS (
                SELECT
                    b.[StockItemId],
                    SUM(b.[CurrentQuantity]) AS [CurrentStock],
                    MIN(b.[ExpiryDate]) AS [EarliestExpiryDate]
                FROM [Batches] b
                INNER JOIN CandidateItems candidates ON candidates.[Id] = b.[StockItemId]
                WHERE b.[IsDeleted] = 0
                  AND b.[IsDepleted] = 0
                GROUP BY b.[StockItemId]
            )
            SELECT TOP (@limit)
                candidates.[Id],
                candidates.[Name],
                candidates.[BaseIngredientId],
                candidates.[DefaultUnitOfMeasureId],
                candidates.[MinimumLevel],
                candidates.[LeadTimeDays],
                COALESCE(totals.[CurrentStock], 0) AS [CurrentStock],
                candidates.[CategoryId],
                candidates.[Category],
                candidates.[UnitSymbol],
                totals.[EarliestExpiryDate]
            FROM CandidateItems candidates
            LEFT JOIN ActiveBatchTotals totals ON totals.[StockItemId] = candidates.[Id]
            WHERE (@onlyAvailable = 0 OR COALESCE(totals.[CurrentStock], 0) > 0)
            ORDER BY
                candidates.[SearchRank],
                candidates.[Name] ASC,
                candidates.[Id] ASC;
            """;
    }

    private static string CreateStockRowsPagedQuerySql(bool includeContains)
    {
        var searchPredicate = includeContains
            ? StockRowsSearchPredicateContainsSql
            : StockRowsSearchPredicatePrefixSql;

        return $"""
            {CreateStockRowsTempTableSql(searchPredicate)}
            {StockRowsFilterCountSql}
            {StockRowsPageSql}
            DROP TABLE #StockRows;
            """;
    }

    private static string CreateStockRowsTempTableSql(string searchPredicate)
    {
        return $"""
            WITH FilteredStockItems AS (
                SELECT
                    si.[Id],
                    si.[Name],
                    si.[BaseIngredientId],
                    si.[DefaultUnitOfMeasureId],
                    si.[MinimumLevel],
                    si.[LeadTimeDays],
                    si.[WarehouseCategoryId] AS [CategoryId],
                    wc.[Name] AS [Category],
                    uom.[Symbol] AS [UnitSymbol]
                FROM [StockItems] si
                INNER JOIN [WarehouseCategories] wc ON wc.[Id] = si.[WarehouseCategoryId]
                INNER JOIN [UnitsOfMeasure] uom ON uom.[Id] = si.[DefaultUnitOfMeasureId]
                WHERE si.[IsDeleted] = 0
                  AND (@categoryId IS NULL OR si.[WarehouseCategoryId] = @categoryId)
                  AND (@legacyCategory IS NULL OR @categoryId IS NOT NULL OR wc.[Name] = @legacyCategory)
                  {searchPredicate}
            ),
            ActiveBatchTotals AS (
                SELECT
                    b.[StockItemId],
                    SUM(b.[CurrentQuantity]) AS [CurrentStock],
                    MIN(b.[ExpiryDate]) AS [EarliestExpiryDate]
                FROM [Batches] b
                INNER JOIN FilteredStockItems fsi ON fsi.[Id] = b.[StockItemId]
                WHERE b.[IsDeleted] = 0
                  AND b.[IsDepleted] = 0
                GROUP BY b.[StockItemId]
            )
            SELECT
                fsi.[Id],
                fsi.[Name],
                fsi.[BaseIngredientId],
                fsi.[DefaultUnitOfMeasureId],
                fsi.[MinimumLevel],
                fsi.[LeadTimeDays],
                COALESCE(totals.[CurrentStock], 0) AS [CurrentStock],
                fsi.[CategoryId],
                fsi.[Category],
                fsi.[UnitSymbol],
                totals.[EarliestExpiryDate]
            INTO #StockRows
            FROM FilteredStockItems fsi
            LEFT JOIN ActiveBatchTotals totals ON totals.[StockItemId] = fsi.[Id];
            """;
    }

    private const string StockLookupSearchPredicatePrefixSql = """
        AND (
                @searchExact IS NULL
                OR si.[Name] = @searchExact
                OR si.[Name] LIKE @searchPrefix
                OR EXISTS (
                    SELECT 1
                    FROM [Batches] searchBatches
                    WHERE searchBatches.[StockItemId] = si.[Id]
                      AND searchBatches.[IsDeleted] = 0
                      AND searchBatches.[IsDepleted] = 0
                      AND (
                            searchBatches.[SupplierBatchNumber] = @searchExact
                            OR searchBatches.[SupplierBatchNumber] LIKE @searchPrefix
                      )
                )
        )
        """;

    private const string StockLookupSearchPredicateContainsSql = """
        AND (
                @searchExact IS NULL
                OR si.[Name] = @searchExact
                OR si.[Name] LIKE @searchPrefix
                OR si.[Name] LIKE @searchContains
                OR EXISTS (
                    SELECT 1
                    FROM [Batches] searchBatches
                    WHERE searchBatches.[StockItemId] = si.[Id]
                      AND searchBatches.[IsDeleted] = 0
                      AND searchBatches.[IsDepleted] = 0
                      AND (
                            searchBatches.[SupplierBatchNumber] = @searchExact
                            OR searchBatches.[SupplierBatchNumber] LIKE @searchPrefix
                            OR searchBatches.[SupplierBatchNumber] LIKE @searchContains
                      )
                )
        )
        """;

    private const string StockRowsSearchPredicatePrefixSql = """
        AND (
                @searchExact IS NULL
                OR si.[Name] = @searchExact
                OR si.[Name] LIKE @searchPrefix
                OR EXISTS (
                    SELECT 1
                    FROM [Batches] searchBatches
                    WHERE searchBatches.[StockItemId] = si.[Id]
                      AND searchBatches.[IsDeleted] = 0
                      AND searchBatches.[IsDepleted] = 0
                      AND (
                            searchBatches.[SupplierBatchNumber] = @searchExact
                            OR searchBatches.[SupplierBatchNumber] LIKE @searchPrefix
                      )
                )
        )
        """;

    private const string StockRowsSearchPredicateContainsSql = """
        AND (
                @searchExact IS NULL
                OR si.[Name] = @searchExact
                OR si.[Name] LIKE @searchPrefix
                OR si.[Name] LIKE @searchContains
                OR EXISTS (
                    SELECT 1
                    FROM [Batches] searchBatches
                    WHERE searchBatches.[StockItemId] = si.[Id]
                      AND searchBatches.[IsDeleted] = 0
                      AND searchBatches.[IsDepleted] = 0
                      AND (
                            searchBatches.[SupplierBatchNumber] = @searchExact
                            OR searchBatches.[SupplierBatchNumber] LIKE @searchPrefix
                            OR searchBatches.[SupplierBatchNumber] LIKE @searchContains
                      )
                )
        )
        """;

    private const string StockRowsWhereSql = """
        WHERE (@showLowStockOnly = 0 OR [CurrentStock] < [MinimumLevel])
          AND (
                (@showExpiredOnly = 0 AND @showExpiringSoonOnly = 0)
                OR (@showExpiredOnly = 1 AND [EarliestExpiryDate] <= @now)
                OR (@showExpiringSoonOnly = 1 AND [EarliestExpiryDate] > @now AND [EarliestExpiryDate] <= @soonCutoff)
          )
        """;

    private const string StockRowsFilterCountSql = $"""
        SELECT COUNT(*)
        FROM #StockRows
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
        FROM #StockRows
        {StockRowsWhereSql}
        ORDER BY [Name] ASC, [Id] ASC
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    private sealed record SearchParameters(
        string? Exact,
        string? Prefix,
        string? Contains,
        bool UseContains);
}


