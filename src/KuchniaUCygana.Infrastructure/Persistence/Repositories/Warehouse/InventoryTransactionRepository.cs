using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

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
        return await db.SelectAsync<InventoryTransaction>(t => t.BatchId == batchId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryTransaction>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var db = Factory.CreateConnection();
        // Kolumna CreatedAt jest obecnie typu datetime w SQL Server, dlatego filtrujemy po UTC DateTime,
        // aby uniknac niejawnych konwersji datetimeoffset <-> datetime podczas porownan granicznych.
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;

        return await db.SelectAsync<InventoryTransaction>(t =>
            t.CreatedAt >= fromUtc &&
            t.CreatedAt <= toUtc);
    }
}
