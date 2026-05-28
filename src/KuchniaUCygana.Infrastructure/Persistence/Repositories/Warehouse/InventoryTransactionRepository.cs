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
}
