using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("PackagingRequirements")]
public sealed class PackagingRequirement : AuditableEntity
{
    [StringLength(40)]
    public string OwnerType { get; set; } = string.Empty;

    public int? MealId { get; set; }

    public int? MealVariantId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    [StringLength(200)]
    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;

    [StringLength(20)]
    public string Unit { get; set; } = "pcs";

    [StringLength(80)]
    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; } = true;
}
