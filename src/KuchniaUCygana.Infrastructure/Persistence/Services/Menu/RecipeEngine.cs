using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Infrastructure.Persistence.Services.Menu;

public sealed class RecipeEngine : IRecipeEngine
{
    private readonly IRecipeRepository recipeRepository;
    private readonly INutritionCalculator nutritionCalculator;
    private readonly IAllergenPropagationService allergenPropagation;

    public RecipeEngine(
        IRecipeRepository recipeRepository,
        INutritionCalculator nutritionCalculator,
        IAllergenPropagationService allergenPropagation)
    {
        this.recipeRepository = recipeRepository;
        this.nutritionCalculator = nutritionCalculator;
        this.allergenPropagation = allergenPropagation;
    }

    public async Task<bool> ValidateRecipeAsync(int mealId)
    {
        var items = await this.recipeRepository.GetByMealIdAsync(mealId);
        var list = items.ToList();
        return list.Count > 0 && list.TrueForAll(r => r.WeightInGrams > 0);
    }

    public async Task RecalculateNutritionAsync(int mealId)
    {
        await this.nutritionCalculator.RecalculateForMealAsync(mealId);
        await this.allergenPropagation.PropagateAllergenAsync(mealId);
    }
}
