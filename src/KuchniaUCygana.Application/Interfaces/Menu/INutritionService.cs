using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface INutritionService
{
    Task<NutritionFactDto?> GetForMealAsync(int mealId);

    Task RecalculateAsync(int mealId);
}
