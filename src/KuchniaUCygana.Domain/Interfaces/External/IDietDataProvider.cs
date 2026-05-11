namespace KuchniaUCygana.Domain.Interfaces.External;

public sealed class DietPlanEntry
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public decimal ServingWeightGrams { get; set; }
}

public sealed class RecipeIngredientEntry
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public decimal WeightInGrams { get; set; }

    public bool IsOptional { get; set; }
}

public interface IDietDataProvider
{
    Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate);

    Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId);
}
