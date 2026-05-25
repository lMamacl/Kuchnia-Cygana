using KuchniaUCygana.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Table("OrderItems")]
public sealed class OrderItem : AuditableEntity
{
    public int OrderId { get; set; }

    public int DietId { get; set; }

    public int DietVariantId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int CaloriesPerDay { get; set; }

    public decimal PricePerDay { get; set; }

    public int TotalDays { get; set; }

    public decimal TotalPrice { get; set; }
}
