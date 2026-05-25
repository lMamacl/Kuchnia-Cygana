using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Table("Orders")]
public sealed class Order : AuditableEntity
{
    public int CustomerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public decimal TotalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalPrice { get; set; }

    public int? DiscountCodeId { get; set; }

    public string? Notes { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [NotMapped]
    public List<OrderItem> Items { get; set; } = new();

    [NotMapped]
    public List<DeliveryCalendar> DeliveryDays { get; set; } = new();
}
