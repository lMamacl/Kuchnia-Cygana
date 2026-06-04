using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public class InventoryTransactionRepository : BaseRepository<InventoryTransaction, long>, IInventoryTransactionRepository
{
    public InventoryTransactionRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryTransaction>> GetByBatchIdAsync(int batchId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<InventoryTransaction>(
            """
            SELECT *
            FROM [InventoryTransactions]
            WHERE [BatchId] = @batchId;
            """,
            new { batchId });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryTransaction>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var db = Factory.CreateConnection();
        // Kolumna CreatedAt jest obecnie typu datetime w SQL Server, dlatego filtrujemy po UTC DateTime,
        // aby uniknac niejawnych konwersji datetimeoffset <-> datetime podczas porownan granicznych.
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;

        return await db.QueryAsync<InventoryTransaction>(
            """
            SELECT *
            FROM [InventoryTransactions]
            WHERE [CreatedAt] >= @fromUtc
              AND [CreatedAt] <= @toUtc
            ORDER BY [CreatedAt], [Id];
            """,
            new { fromUtc, toUtc });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryTransaction>> GetByStockItemIdAsync(int stockItemId, int page = 1, int pageSize = 25)
    {
        using var db = Factory.CreateConnection();
        var offset = (page - 1) * pageSize;

        return await db.QueryAsync<InventoryTransaction>(
            """
            SELECT *
            FROM [InventoryTransactions]
            WHERE [StockItemId] = @stockItemId
            ORDER BY [CreatedAt] DESC, [Id] DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """,
            new { stockItemId, offset, pageSize });
    }

    public async Task<(IEnumerable<TransactionHistoryRow> Items, int TotalCount)> GetTransactionHistoryPageAsync(
        TransactionHistoryQuery query)
    {
        using var db = Factory.CreateConnection();

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;
        var parameters = new
        {
            stockItemId = query.StockItemId,
            fromUtc = query.From?.UtcDateTime,
            toUtc = query.To?.UtcDateTime,
            transactionType = query.TransactionType,
            offset,
            pageSize,
        };

        var totalCount = await db.ExecuteScalarAsync<int>($"{TransactionRowsCte} {TransactionRowsCountSql}", parameters);
        var items = await db.QueryAsync<TransactionHistoryRow>($"{TransactionRowsCte} {TransactionRowsPageSql}", parameters);

        return (items, totalCount);
    }

    private const string TransactionRowsCte = """
        WITH TransactionRows AS (
            SELECT
                it.[Id],
                COALESCE(it.[StockItemId], b.[StockItemId]) AS [StockItemId],
                COALESCE(si.[Name], 'Nieznany') AS [StockItemName],
                it.[BatchId],
                COALESCE(b.[SupplierBatchNumber], 'Nieznana') AS [BatchNumber],
                it.[TransactionType],
                it.[QuantityChanged] AS [Quantity],
                it.[CreatedAt] AS [PerformedAt],
                COALESCE(it.[Reason], '') AS [Reason],
                COALESCE(it.[ReferenceDocument], '') AS [ReferenceDocument]
            FROM [InventoryTransactions] it
            LEFT JOIN [Batches] b ON b.[Id] = it.[BatchId]
            LEFT JOIN [StockItems] si ON si.[Id] = COALESCE(it.[StockItemId], b.[StockItemId])
            WHERE (@stockItemId IS NULL OR it.[StockItemId] = @stockItemId)
              AND (@transactionType IS NULL OR it.[TransactionType] = @transactionType)
              AND (@fromUtc IS NULL OR it.[CreatedAt] >= @fromUtc)
              AND (@toUtc IS NULL OR it.[CreatedAt] <= @toUtc)
        )
        """;

    private const string TransactionRowsCountSql = """
        SELECT COUNT(*)
        FROM TransactionRows;
        """;

    private const string TransactionRowsPageSql = """
        SELECT *
        FROM TransactionRows
        ORDER BY [PerformedAt] DESC, [Id] DESC
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;
}
