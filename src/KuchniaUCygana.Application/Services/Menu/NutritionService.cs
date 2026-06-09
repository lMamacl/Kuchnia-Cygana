using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class NutritionService : INutritionService
{
    private readonly INutritionFactRepository nutritionFactRepository;
    private readonly INutritionCalculator nutritionCalculator;

    public NutritionService(
        INutritionFactRepository nutritionFactRepository,
        INutritionCalculator nutritionCalculator)
    {
        this.nutritionFactRepository = nutritionFactRepository;
        this.nutritionCalculator = nutritionCalculator;
    }

    public async Task<NutritionFactDto?> GetForMealAsync(int mealId)
    {
        var fact = await this.nutritionFactRepository.GetForMealAsync(mealId);
        return fact is null
            ? null
            : new NutritionFactDto
            {
                Id = fact.Id,
                CaloriesPer100g = fact.CaloriesPer100g,
                ProteinPer100g = fact.ProteinPer100g,
                CarbohydratesPer100g = fact.CarbohydratesPer100g,
                FatPer100g = fact.FatPer100g,
                FiberPer100g = fact.FiberPer100g
            };
    }

    public async Task RecalculateAsync(int mealId)
    {
        await this.nutritionCalculator.RecalculateForMealAsync(mealId);
    }
}
