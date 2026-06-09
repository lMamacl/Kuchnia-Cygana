using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class FefoReportItemDto
{
    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public int BatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public DateTimeOffset? ExpiryDate { get; set; }

    public decimal Quantity { get; set; }

    public int? DaysToExpiry { get; set; }

    public string Status { get; set; } = string.Empty; // Expired, Critical, Warning, Safe
}
