using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IBatchRepository : IRepository<Batch>
{
    /// <summary>
    /// Zwraca aktywne (nie wyczerpane) partie dla danego składnika,
    /// posortowane FEFO (First Expired, First Out).
    /// </summary>
    Task<IEnumerable<Batch>> GetActiveBatchesByStockItemAsync(int stockItemId);

    /// <summary>
    /// Zwraca partie, których data ważności upływa przed podaną datą.
    /// Używane do alertów o zbliżającym się przeterminowaniu.
    /// </summary>
    Task<IEnumerable<Batch>> GetExpiringBeforeAsync(DateTimeOffset date);
}
