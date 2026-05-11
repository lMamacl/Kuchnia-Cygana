using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public List<DeliveryCalendarDto> DeliveryDays { get; set; } = new();
}
