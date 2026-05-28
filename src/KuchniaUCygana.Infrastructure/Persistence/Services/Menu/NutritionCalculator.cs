using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Infrastructure.Persistence.Services.Menu;

public sealed class NutritionCalculator : INutritionCalculator
{
    private readonly IRecipeRepository recipeRepository;
    private readonly INutritionFactRepository nutritionFactRepository;

    public NutritionCalculator(
        IRecipeRepository recipeRepository,
        INutritionFactRepository nutritionFactRepository)
    {
        this.recipeRepository = recipeRepository;
        this.nutritionFactRepository = nutritionFactRepository;
    }

    public async Task RecalculateForMealAsync(int mealId)
    {
        var recipes = await this.recipeRepository.GetByMealIdAsync(mealId);
        var recipeList = recipes.ToList();

        if (recipeList.Count == 0)
        {
            return;
        }

        decimal totalWeight = 0, totalCal = 0, totalPro = 0, totalCarb = 0, totalFat = 0, totalFib = 0;

        foreach (var recipe in recipeList)
        {
            var ingNutrition = await this.nutritionFactRepository.GetForIngredientAsync(recipe.IngredientId);
            if (ingNutrition is null)
            {
                continue;
            }

            var factor = recipe.WeightInGrams / 100m;
            totalWeight += recipe.WeightInGrams;
            totalCal += ingNutrition.CaloriesPer100g * factor;
            totalPro += ingNutrition.ProteinPer100g * factor;
            totalCarb += ingNutrition.CarbohydratesPer100g * factor;
            totalFat += ingNutrition.FatPer100g * factor;
            totalFib += ingNutrition.FiberPer100g * factor;
        }

        if (totalWeight == 0)
        {
            return;
        }

        var existing = await this.nutritionFactRepository.GetForMealAsync(mealId);
        if (existing is not null)
        {
            existing.CaloriesPer100g = totalCal * 100 / totalWeight;
            existing.ProteinPer100g = totalPro * 100 / totalWeight;
            existing.CarbohydratesPer100g = totalCarb * 100 / totalWeight;
            existing.FatPer100g = totalFat * 100 / totalWeight;
            existing.FiberPer100g = totalFib * 100 / totalWeight;
            await this.nutritionFactRepository.UpdateAsync(existing);
        }
        else
        {
            var newFact = new NutritionFact
            {
                MealId = mealId,
                CaloriesPer100g = totalCal * 100 / totalWeight,
                ProteinPer100g = totalPro * 100 / totalWeight,
                CarbohydratesPer100g = totalCarb * 100 / totalWeight,
                FatPer100g = totalFat * 100 / totalWeight,
                FiberPer100g = totalFib * 100 / totalWeight,
            };
            await this.nutritionFactRepository.InsertAsync(newFact);
        }
    }

    public Task RecalculateForIngredientAsync(int ingredientId)
    {
        return Task.CompletedTask;
    }
}
