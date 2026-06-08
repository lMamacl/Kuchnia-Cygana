using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IDietMenuPlanManagementService
{
    Task<DietMenuWeekDto> GetWeekAsync(DateOnly startDate, int days = 7);

    Task<DietMenuDayDto> GetDayAsync(DateOnly date);

    Task<DietMenuDayShellDto> GetDayShellAsync(DateOnly date);

    Task<DietMenuDietVariantItemsDto> GetDietVariantItemsAsync(int planId, int dietVariantId);

    Task<int> CreateDayAsync(CreateDietMenuPlanRequest request);

    Task AddItemAsync(AddDietMenuPlanItemRequest request);

    Task UpdateItemAsync(UpdateDietMenuPlanItemRequest request);

    Task DeleteItemAsync(int itemId);

    Task CopyDayAsync(CopyDietMenuDayRequest request);

    Task PublishAsync(PublishDietMenuPlanRequest request);
}
