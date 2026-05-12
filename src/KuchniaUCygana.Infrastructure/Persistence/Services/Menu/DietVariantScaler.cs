using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Services.Menu;

public sealed class DietVariantScaler : IDietVariantScaler
{
    private readonly IDbConnectionFactory connectionFactory;
    private readonly IDietVariantRepository dietVariantRepository;
    private readonly IMealRepository mealRepository;
    private readonly INutritionFactRepository nutritionFactRepository;

    public DietVariantScaler(
        IDbConnectionFactory connectionFactory,
        IDietVariantRepository dietVariantRepository,
        IMealRepository mealRepository,
        INutritionFactRepository nutritionFactRepository)
    {
        this.connectionFactory = connectionFactory;
        this.dietVariantRepository = dietVariantRepository;
        this.mealRepository = mealRepository;
        this.nutritionFactRepository = nutritionFactRepository;
    }

    public async Task RecalculateVariantAsync(int dietVariantId)
    {
        var variant = await this.dietVariantRepository.GetByIdAsync(dietVariantId);
        if (variant is null)
        {
            return;
        }

        using var db = this.connectionFactory.CreateConnection();
        var variantMeals = await db.SelectAsync<DietVariantMeal>(
            dvm => dvm.DietVariantId == dietVariantId);

        if (!variantMeals.Any())
        {
            return;
        }

        decimal totalBaseCalories = 0;
        foreach (var vm in variantMeals)
        {
            var nutrition = await this.nutritionFactRepository.GetForMealAsync(vm.MealId);
            if (nutrition is not null)
            {
                totalBaseCalories += nutrition.CaloriesPer100g;
            }
        }

        if (totalBaseCalories > 0)
        {
            var multiplier = variant.TargetCalories / totalBaseCalories;
            foreach (var vm in variantMeals)
            {
                vm.ServingSizeMultiplier = multiplier;
                await db.UpdateAsync(vm);
            }
        }
    }

    public decimal ScaleMealWeight(decimal baseWeightGrams, decimal multiplier)
    {
        return baseWeightGrams * multiplier;
    }
}
