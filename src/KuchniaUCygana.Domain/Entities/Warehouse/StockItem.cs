using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("StockItems")]
public class StockItem : AuditableEntity<int>
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    // This acts as a bridge identifier to Module 2's Ingredient without strict foreign keys
    public int? BaseIngredientId { get; set; }
    public int DefaultUnitOfMeasureId { get; set; }

    public int WarehouseCategoryId { get; set; } = 4;

    public decimal MinimumLevel { get; set; }

    public int LeadTimeDays { get; set; }
}
