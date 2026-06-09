using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class BatchDetailsDto
{
    public int Id { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public string ReceivedBy { get; set; } = string.Empty;

    public bool IsExpired { get; set; }

    public int? DaysToExpiry { get; set; }
}
