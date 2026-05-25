using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("IngredientAllergens")]
public sealed class IngredientAllergen
{
    [Key]
    public int Id { get; set; }

    public int IngredientId { get; set; }

    public int AllergenId { get; set; }

    public bool TraceAmount { get; set; } = false;
}
