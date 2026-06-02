using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("MealVariantComponents")]
public sealed class MealVariantComponent : AuditableEntity
{
    public int MealVariantId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    [StringLength(80)]
    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; } = 1.0m;

    [StringLength(40)]
    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public bool IsOptional { get; set; }
}
