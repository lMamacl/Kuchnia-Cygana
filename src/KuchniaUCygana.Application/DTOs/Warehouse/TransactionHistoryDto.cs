using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class TransactionHistoryDto
{
    public int Id { get; set; }

    public int? StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public int BatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public DateTimeOffset PerformedAt { get; set; }

    public string PerformedBy { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
