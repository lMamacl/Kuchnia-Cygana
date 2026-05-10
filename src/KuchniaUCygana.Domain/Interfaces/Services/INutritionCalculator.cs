namespace KuchniaUCygana.Domain.Interfaces.Services.Menu;

public interface INutritionCalculator
{
    Task RecalculateForMealAsync(int mealId);

    Task RecalculateForIngredientAsync(int ingredientId);
}
