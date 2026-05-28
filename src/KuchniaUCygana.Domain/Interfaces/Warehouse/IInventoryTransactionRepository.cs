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

    /// <summary>
    /// Historia transakcji dla konkretnego składnika magazynowego (przez StockItemId).
    /// Wymaga migracji 010 (backfill StockItemId). Stronicowana, posortowana malejąco po dacie.
    /// </summary>
    Task<IEnumerable<InventoryTransaction>> GetByStockItemIdAsync(int stockItemId, int page = 1, int pageSize = 25);
}

