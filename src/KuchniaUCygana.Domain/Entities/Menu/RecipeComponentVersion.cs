using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("RecipeComponentVersions")]
public sealed class RecipeComponentVersion : AuditableEntity
{
    public int RecipeComponentId { get; set; }

    public int VersionNumber { get; set; } = 1;

    [StringLength(30)]
    public string Status { get; set; } = "Draft";

    [StringLength(4000)]
    public string? Instructions { get; set; }

    public decimal YieldQuantity { get; set; } = 1.0m;

    [StringLength(40)]
    public string YieldUnit { get; set; } = "portion";

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    [StringLength(500)]
    public string? ChangeSummary { get; set; }

    public bool IsTechnologyChange { get; set; } = true;

    [StringLength(500)]
    public string? NonTechnologyChangeReason { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    [StringLength(100)]
    public string? PublishedBy { get; set; }
}
