using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Infrastructure.Persistence.Services.Menu;

public sealed class RecipeEngine : IRecipeEngine
{
    private readonly IRecipeRepository recipeRepository;
    private readonly IRecipeComponentRepository recipeComponentRepository;
    private readonly INutritionCalculator nutritionCalculator;
    private readonly IAllergenPropagationService allergenPropagation;

    public RecipeEngine(
        IRecipeRepository recipeRepository,
        IRecipeComponentRepository recipeComponentRepository,
        INutritionCalculator nutritionCalculator,
        IAllergenPropagationService allergenPropagation)
    {
        this.recipeRepository = recipeRepository;
        this.recipeComponentRepository = recipeComponentRepository;
        this.nutritionCalculator = nutritionCalculator;
        this.allergenPropagation = allergenPropagation;
    }

    public async Task<bool> ValidateRecipeAsync(int mealId)
    {
        var componentRows = (await this.recipeComponentRepository.GetMealComponentDetailsAsync(mealId)).ToList();
        if (componentRows.Count > 0)
        {
            var hasPackaging = await this.recipeComponentRepository.HasProductionPackagingAsync(mealId);
            return hasPackaging
                && componentRows.TrueForAll(r =>
                    r.IngredientId > 0
                    && r.WeightInGrams > 0
                    && r.WarehouseCategoryId.HasValue);
        }

        var items = (await this.recipeRepository.GetByMealIdAsync(mealId)).ToList();
        return items.Count > 0 && items.TrueForAll(r => r.WeightInGrams > 0);
    }

    public async Task RecalculateNutritionAsync(int mealId)
    {
        await this.nutritionCalculator.RecalculateForMealAsync(mealId);
        await this.allergenPropagation.PropagateAllergenAsync(mealId);
    }
}
