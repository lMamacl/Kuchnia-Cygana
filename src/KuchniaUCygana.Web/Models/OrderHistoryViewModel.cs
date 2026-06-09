using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Web.Models;

public sealed class OrderHistoryViewModel
{
    public OrderHistoryPageDto Page { get; set; } = new();

    public OrderStatus? Status { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public string? OrderNumber { get; set; }

    public int PageSize { get; set; } = 10;

    public bool HasActiveFilters =>
        Status.HasValue ||
        DateFrom.HasValue ||
        DateTo.HasValue ||
        !string.IsNullOrWhiteSpace(OrderNumber);
}
