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

    /// <summary>
    /// Pobiera składnik wraz z jego aktywnymi partiami (denormalizowane).
    /// Używane przez widok BatchDetails do wyświetlenia partii i historii.
    /// </summary>
    Task<StockItem?> GetWithBatchesAsync(int stockItemId);

    /// <summary>
    /// Stronicowana lista składników z filtrowaniem po nazwie i kategorii.
    /// Używane przez Warehouse/Index z HTMX + paginacją.
    /// </summary>
    /// <param name="filter">Filtr: SearchTerm (nazwa), Page, PageSize (domyślnie 25).</param>
    Task<(IEnumerable<StockItem> Items, int TotalCount)> GetPagedAsync(StockItemFilter filter);
}

/// <summary>
/// Parametry filtrowania listy składników magazynowych.
/// </summary>
public record StockItemFilter(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 25);
