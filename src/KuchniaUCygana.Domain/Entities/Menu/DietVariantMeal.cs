using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("DietVariantMeals")]
public sealed class DietVariantMeal
{
    [Key]
    public int Id { get; set; }

    public int DietVariantId { get; set; }

    public int MealId { get; set; }

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public int SortOrder { get; set; }
}
