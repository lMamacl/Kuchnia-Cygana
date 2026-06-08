using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class OrderHistoryQueryDto
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public OrderStatus? Status { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public string? OrderNumber { get; set; }
}

public sealed class OrderHistoryPageDto
{
    public IReadOnlyList<OrderSummaryDto> Items { get; set; } = Array.Empty<OrderSummaryDto>();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, TotalCount);

    public int PreviousPage => Math.Max(1, Page - 1);

    public int NextPage => Math.Min(TotalPages, Page + 1);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
