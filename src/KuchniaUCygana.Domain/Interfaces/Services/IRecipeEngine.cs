namespace KuchniaUCygana.Domain.Interfaces.Services.Menu;

public interface IRecipeEngine
{
    Task<bool> ValidateRecipeAsync(int mealId);

    Task RecalculateNutritionAsync(int mealId);
}
