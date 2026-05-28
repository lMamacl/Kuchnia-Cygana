using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class OrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal FinalPrice { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ItemCount { get; set; }
}
