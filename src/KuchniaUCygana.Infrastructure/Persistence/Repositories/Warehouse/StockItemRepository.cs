using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

/// <summary>
/// Repozytorium StockItem z metodami Smart Inventory.
/// </summary>
public sealed class StockItemRepository : BaseRepository<StockItem>, IStockItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockItemRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
        // Uwaga: partie pobierane są osobno przez IBatchRepository.GetActiveBatchesByStockItemAsync
        // aby uniknąć złożonego multi-mapping z Dapper dla rzadko potrzebnych danych.
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<StockItem> Items, int TotalCount)> GetPagedAsync(StockItemFilter filter)
    {
        using var db = _connectionFactory.CreateConnection();

        var searchParam = string.IsNullOrWhiteSpace(filter.SearchTerm)
            ? null
            : $"%{filter.SearchTerm.Trim()}%";
        var offset = (filter.Page - 1) * filter.PageSize;

        const string countSql = """
            SELECT COUNT(*)
            FROM [StockItems]
            WHERE [IsDeleted] = 0
              AND (@search IS NULL OR [Name] LIKE @search);
            """;

        const string itemsSql = """
            SELECT *
            FROM [StockItems]
            WHERE [IsDeleted] = 0
              AND (@search IS NULL OR [Name] LIKE @search)
            ORDER BY [Name] ASC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        var parameters = new { search = searchParam, offset, pageSize = filter.PageSize };

        var totalCount = await db.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await db.QueryAsync<StockItem>(itemsSql, parameters);

        return (items, totalCount);
    }
}
