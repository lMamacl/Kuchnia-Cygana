using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IMealVariantRepository
{
    Task<IReadOnlyList<MealVariantRow>> GetByMealIdAsync(int mealId);

    Task<MealVariantRow?> GetByIdAsync(int mealVariantId);

    Task<MealVariantRow?> GetDefaultByMealIdAsync(int mealId);

    Task<int> CreateAsync(MealVariant variant, int? sourceMealVariantId, string? userName);

    Task<IReadOnlyList<MealVariantComponentRow>> GetComponentsAsync(int mealVariantId);
}

public sealed class MealVariantRow
{
    public int Id { get; set; }

    public int MealId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string VariantType { get; set; } = "Standard";

    public string Status { get; set; } = "Draft";

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public string NutritionSource { get; set; } = "Aggregated";

    public string? NutritionOverrideReason { get; set; }

    public bool AllergensApproved { get; set; }

    public string? AllergenOverrideReason { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }
}

public sealed class MealVariantComponentRow
{
    public int Id { get; set; }

    public int MealVariantId { get; set; }

    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public bool IsOptional { get; set; }
}
