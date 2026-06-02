using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("MealVariants")]
public sealed class MealVariant : AuditableEntity
{
    public int MealId { get; set; }

    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    public string VariantType { get; set; } = "Standard";

    [StringLength(30)]
    public string Status { get; set; } = "Draft";

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    [StringLength(40)]
    public string NutritionSource { get; set; } = "Aggregated";

    [StringLength(500)]
    public string? NutritionOverrideReason { get; set; }

    public bool AllergensApproved { get; set; }

    [StringLength(500)]
    public string? AllergenOverrideReason { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    [StringLength(100)]
    public string? PublishedBy { get; set; }
}
