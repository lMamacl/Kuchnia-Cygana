using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IStockItemRepository : IRepository<StockItem>
{
    /// <summary>
    /// Składniki poniżej poziomu minimum — do alertów Smart Inventory.
    /// </summary>
    Task<IEnumerable<StockItem>> GetBelowMinimumAsync();

    /// <summary>
    /// Wyszukanie składnika po ID składnika z Modułu 2.
    /// </summary>
    Task<StockItem?> GetByIngredientIdAsync(int baseIngredientId);
}
