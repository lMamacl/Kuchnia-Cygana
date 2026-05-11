using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class NutritionFact : BaseEntity
{
    public int? MealId { get; set; }

    public int? IngredientId { get; set; }

    public decimal CaloriesPer100g { get; set; }

    public decimal ProteinPer100g { get; set; }

    public decimal CarbohydratesPer100g { get; set; }

    public decimal FatPer100g { get; set; }

    public decimal FiberPer100g { get; set; }
}
