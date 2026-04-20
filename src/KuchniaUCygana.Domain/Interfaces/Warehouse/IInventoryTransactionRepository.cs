using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IInventoryTransactionRepository : IRepository<InventoryTransaction, long>
{
    /// <summary>
    /// Historia transakcji dla konkretnej partii.
    /// </summary>
    Task<IEnumerable<InventoryTransaction>> GetByBatchIdAsync(int batchId);

    /// <summary>
    /// Logi z okresu – potrzebne do raportów Sanepidu za dany okres.
    /// </summary>
    Task<IEnumerable<InventoryTransaction>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to);
}
