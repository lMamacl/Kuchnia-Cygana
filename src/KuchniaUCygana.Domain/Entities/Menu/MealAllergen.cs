using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class MealAllergen
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int MealId { get; set; }

    public int AllergenId { get; set; }

    public bool IsTrace { get; set; } = false;
}
