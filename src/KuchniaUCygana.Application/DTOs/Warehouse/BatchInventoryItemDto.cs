using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class BatchInventoryItemDto
{
    public int BatchId { get; set; }

    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string CategoryName
    {
        get => Category;
        set => Category = value;
    }

    public decimal CurrentQuantity { get; set; }

    public string UnitSymbol { get; set; } = string.Empty;

    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset ReceivedDate { get; set; }
}
