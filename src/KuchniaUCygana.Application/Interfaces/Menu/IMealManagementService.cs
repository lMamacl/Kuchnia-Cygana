using KuchniaUCygana.Application.DTOs.Menu;

using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IMealManagementService
{
    Task<PagedResultDto<MealListItemDto>> SearchAsync(MealSearchFilterDto filter);

    Task<IReadOnlyList<MenuPlanMealLookupDto>> SearchPlanningMealsAsync(string? query, int limit = 20);

    Task<MealDto?> GetMealAsync(int mealId);

    Task<MealDetailDto?> GetMealWithDetailsAsync(int mealId);

    Task<MealVariantResultDto?> GetMealVariantResultAsync(int mealId, int? mealVariantId);

    Task<IReadOnlyDictionary<MealVariantResultKey, MealVariantResultDto?>> GetMealVariantResultsAsync(
        IEnumerable<MealVariantResultKey> keys,
        MealVariantResultCacheMode cacheMode = MealVariantResultCacheMode.CachePreferred);

    Task<IReadOnlyList<MealVariantPlanOptionDto>> GetMealVariantOptionsAsync(int mealId);

    Task<IReadOnlyList<MenuPlanMealVariantLookupDto>> GetPlanningMealVariantOptionsAsync(int mealId);

    Task<IEnumerable<MealDto>> GetPublishedMealsAsync();

    Task<MealDto> CreateMealAsync(CreateMealRequest request);

    Task<int> CreateMealVariantAsync(CreateMealVariantRequest request);

    Task UpdateMealVariantAsync(int mealVariantId, UpdateMealVariantRequest request);

    Task SaveMealVariantComponentAsync(int mealVariantId, SaveMealVariantComponentRequest request);

    Task DeleteMealVariantComponentAsync(int componentId);

    Task SaveMealVariantPackagingAsync(int mealVariantId, SaveMealVariantPackagingRequest request);

    Task DeleteMealVariantPackagingAsync(int mealVariantId, int packagingRequirementId);

    Task UpdateMealAsync(int mealId, UpdateMealRequest request);

    Task DeleteMealAsync(int mealId);

    Task PublishMealAsync(int mealId);

    Task PublishMealVariantAsync(int mealVariantId);

    Task ArchiveMealAsync(int mealId);

    Task ArchiveMealVariantAsync(int mealVariantId);
}
