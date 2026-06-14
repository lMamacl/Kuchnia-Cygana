using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IMealRepository : IRepository<Meal>
{
    Task<MealSearchResult> SearchAsync(MealSearchQuery query);

    Task<IReadOnlyList<MealListRow>> SearchPlanningAsync(string? query, int limit);

    Task<IEnumerable<Meal>> GetPublishedAsync();

    Task<Meal?> GetWithRecipeAsync(int mealId);

    Task<MealNutritionCost?> GetMealNutritionCostAsync(int mealId);
}

public sealed class MealSearchQuery
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public string? Status { get; set; }

    public int? AllergenId { get; set; }

    public bool MissingPublicationData { get; set; }

    public bool? HasVariants { get; set; }

    public bool MissingPackaging { get; set; }

    public bool PlanningEligibleOnly { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class MealSearchResult
{
    public IReadOnlyList<MealListRow> Items { get; set; } = Array.Empty<MealListRow>();

    public int TotalCount { get; set; }
}

public sealed class MealListRow
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string Status { get; set; } = string.Empty;

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public int VariantCount { get; set; }

    public int PublishedVariantCount { get; set; }

    public int ComponentCount { get; set; }

    public int LegacyRecipeCount { get; set; }

    public int PackagingRequirementCount { get; set; }

    public string? AllergenNames { get; set; }

    public bool HasNutrition { get; set; }

    public bool MissingPackaging { get; set; }

    public bool MissingPublicationData { get; set; }
}

public sealed class MealNutritionCost
{
    public int MealId { get; set; }

    public decimal EstimatedCost { get; set; }

    public decimal Calories { get; set; }

    public decimal Protein { get; set; }

    public decimal Carbohydrates { get; set; }

    public decimal Fat { get; set; }

    public decimal Fiber { get; set; }
}
