using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Alias("Orders")]
public sealed class Order : AuditableEntity
{
    public int CustomerId { get; set; }

    [Index(Unique = true)]
    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public decimal TotalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalPrice { get; set; }

    public int? DiscountCodeId { get; set; }

    public string? Notes { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Ignore]
    public List<OrderItem> Items { get; set; } = new();

    [Ignore]
    public List<DeliveryCalendar> DeliveryDays { get; set; } = new();
}
