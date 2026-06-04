using KuchniaUCygana.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("Ingredients")]
public sealed class Ingredient : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string ResourceType { get; set; } = "Food";

    public int? FoodCategoryId { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(2000)]
    public string? ProductComposition { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public bool WarehouseCategoryFefoApproved { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsActive { get; set; } = true;
}
