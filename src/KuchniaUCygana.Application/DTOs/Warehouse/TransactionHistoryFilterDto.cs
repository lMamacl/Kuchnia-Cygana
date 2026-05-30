using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class TransactionHistoryFilterDto
{
    public int? StockItemId { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int? TransactionType { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public int TotalCount { get; set; }
}
