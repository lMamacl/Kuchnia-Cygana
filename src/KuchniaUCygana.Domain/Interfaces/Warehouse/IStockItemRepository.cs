using System;
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

    Task<(IEnumerable<StockItemStockRow> Items, int TotalCount)> GetStockTablePageAsync(StockItemTableQuery query);

    Task<IEnumerable<StockItemStockRow>> SearchStockLookupAsync(string query, int limit, bool onlyAvailable);

    Task<StockItemStockRow?> GetStockLookupByIdAsync(int stockItemId);

    Task<IEnumerable<SmartInventoryAlertRow>> GetSmartInventoryAlertRowsAsync(DateTimeOffset now);
}

/// <summary>
/// Parametry filtrowania listy składników magazynowych.
/// </summary>
public record StockItemFilter(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 25);

public sealed record StockItemTableQuery(
    string? Search,
    string? Category,
    bool ShowExpiredOnly,
    bool ShowLowStockOnly,
    bool ShowExpiringSoonOnly,
    int Page,
    int PageSize);

public sealed class StockItemStockRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? BaseIngredientId { get; set; }

    public int DefaultUnitOfMeasureId { get; set; }

    public decimal MinimumLevel { get; set; }

    public int LeadTimeDays { get; set; }

    public decimal CurrentStock { get; set; }

    public string Category { get; set; } = string.Empty;

    public string UnitSymbol { get; set; } = string.Empty;

    public DateTimeOffset? EarliestExpiryDate { get; set; }
}

public sealed class SmartInventoryAlertRow
{
    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public string? SupplierBatchNumber { get; set; }

    public string AlertCode { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public decimal? MinimumLevel { get; set; }

    public DateTimeOffset? EarliestExpiry { get; set; }

    public int? DaysUntilExpiry { get; set; }
}
