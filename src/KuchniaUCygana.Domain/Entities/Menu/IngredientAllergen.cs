using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class IngredientAllergen
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int IngredientId { get; set; }

    public int AllergenId { get; set; }

    public bool TraceAmount { get; set; } = false;
}
