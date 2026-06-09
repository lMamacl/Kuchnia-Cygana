using KuchniaUCygana.Domain.Services;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// DTO alertu magazynowego — wynik SmartInventoryAnalyzer.
/// </summary>
public sealed class InventoryAlertDto
{
    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public string? SupplierBatchNumber { get; set; }

    public string AlertType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public decimal? MinimumLevel { get; set; }

    public DateTimeOffset? EarliestExpiry { get; set; }

    public int? DaysUntilExpiry { get; set; }
}
