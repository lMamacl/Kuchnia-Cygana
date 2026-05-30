namespace KuchniaUCygana.Domain.Interfaces.External;

public sealed class DietPlanEntry
{
    public DateOnly PlanDate { get; set; }

    public string PlanStatus { get; set; } = string.Empty;

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int DietVariantId { get; set; }

    public string MealSlot { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public decimal ServingMultiplier { get; set; } = 1.0m;

    public decimal ServingWeightGrams { get; set; }
}

public sealed class RecipeIngredientEntry
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public string? WarehouseCategoryCode { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsOptional { get; set; }
}

public sealed class NutritionFactsEntry
{
    public decimal CaloriesPer100g { get; set; }

    public decimal ProteinPer100g { get; set; }

    public decimal CarbohydratesPer100g { get; set; }

    public decimal FatPer100g { get; set; }

    public decimal FiberPer100g { get; set; }
}

public sealed class MealCookingDetailsEntry
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? Description { get; set; }

    public string? PreparationInstructions { get; set; }

    public string? MainImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public NutritionFactsEntry? NutritionFacts { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = Array.Empty<string>();
}

public interface IDietDataProvider
{
    Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate);

    Task<IEnumerable<DietPlanEntry>> GetPlanForDateAsync(DateOnly date);

    Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId);

    Task<MealCookingDetailsEntry?> GetMealCookingDetailsAsync(int mealId);
}
