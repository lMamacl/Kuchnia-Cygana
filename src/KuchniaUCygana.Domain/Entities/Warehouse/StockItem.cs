using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Alias("StockItems")]
public class StockItem : AuditableEntity<int>
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    // This acts as a bridge identifier to Module 2's Ingredient without strict foreign keys
    public int? BaseIngredientId { get; set; }

    [References(typeof(UnitOfMeasure))]
    public int DefaultUnitOfMeasureId { get; set; }

    public decimal MinimumLevel { get; set; }

    public int LeadTimeDays { get; set; }
}
