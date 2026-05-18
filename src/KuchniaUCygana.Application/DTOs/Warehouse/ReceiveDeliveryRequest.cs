namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// Request przyjęcia dostawy — tworzy nową partię w magazynie.
/// </summary>
public sealed class ReceiveDeliveryRequest
{
    /// <summary>
    /// ID składnika magazynowego, którego dotyczy dostawa.
    /// </summary>
    public int StockItemId { get; set; }

    /// <summary>
    /// Numer partii od dostawcy.
    /// </summary>
    public string SupplierBatchNumber { get; set; } = string.Empty;

    /// <summary>
    /// Przyjęta ilość.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Data ważności partii.
    /// </summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    /// <summary>
    /// Uwagi do dostawy (opcjonalne).
    /// </summary>
    public string? Notes { get; set; }
}
