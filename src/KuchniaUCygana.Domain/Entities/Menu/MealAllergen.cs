using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("MealAllergens")]
public sealed class MealAllergen
{
    [Key]
    public int Id { get; set; }

    public int MealId { get; set; }

    public int AllergenId { get; set; }

    public bool IsTrace { get; set; } = false;
}
