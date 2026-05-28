using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IBatchExpiryChangeLogRepository : IRepository<BatchExpiryChangeLog>
{
    /// <summary>
    /// Pobiera wszystkie logi zmian dat ważności dla danej partii,
    /// posortowane malejąco po dacie zmiany.
    /// </summary>
    Task<IEnumerable<BatchExpiryChangeLog>> GetByBatchIdAsync(int batchId);

    /// <summary>
    /// Pobiera wszystkie logi zmian dla danego składnika (przez jego partie),
    /// posortowane malejąco po dacie zmiany.
    /// </summary>
    Task<IEnumerable<BatchExpiryChangeLog>> GetByStockItemIdAsync(int stockItemId);
}
