using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IMealManagementService
{
    Task<MealDto?> GetMealAsync(int mealId);

    Task<MealDetailDto?> GetMealWithDetailsAsync(int mealId);

    Task<IEnumerable<MealDto>> GetPublishedMealsAsync();

    Task<MealDto> CreateMealAsync(CreateMealRequest request);

    Task UpdateMealAsync(int mealId, UpdateMealRequest request);

    Task DeleteMealAsync(int mealId);

    Task PublishMealAsync(int mealId);

    Task ArchiveMealAsync(int mealId);
}
