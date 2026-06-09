using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

internal static class MenuPlanningCacheKeys
{
    public const string MealResultPrefix = "menu:meal-result:";

    public static string MealResult(MealVariantResultKey key)
    {
        return $"{MealResultPrefix}{key.MealId}:{key.MealVariantId?.ToString() ?? "base"}";
    }
}
