using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("RecipeComponentIngredients")]
public sealed class RecipeComponentIngredient : AuditableEntity
{
    public int RecipeComponentVersionId { get; set; }

    public int IngredientId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool IsOptional { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
