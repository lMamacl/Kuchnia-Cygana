using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("MealVariantAllergens")]
public sealed class MealVariantAllergen
{
    [Key]
    public int Id { get; set; }

    public int MealVariantId { get; set; }

    public int AllergenId { get; set; }

    public bool IsTrace { get; set; }

    [StringLength(40)]
    public string SourceType { get; set; } = "Aggregated";
}
