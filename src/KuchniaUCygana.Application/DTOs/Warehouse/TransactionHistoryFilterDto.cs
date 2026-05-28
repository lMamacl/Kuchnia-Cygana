using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class TransactionHistoryFilterDto
{
    public int? StockItemId { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int? TransactionType { get; set; }
}
