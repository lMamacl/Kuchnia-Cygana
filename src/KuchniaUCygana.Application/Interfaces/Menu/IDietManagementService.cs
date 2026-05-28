using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IDietManagementService
{
    Task<DietDto?> GetDietAsync(int dietId);

    Task<IEnumerable<DietDto>> GetActiveDietsAsync();

    Task<DietDto> CreateDietAsync(CreateDietRequest request);

    Task UpdateDietAsync(int dietId, UpdateDietRequest request);

    Task AddVariantAsync(int dietId, CreateDietVariantRequest request);

    Task AssignMealToVariantAsync(int variantId, int mealId, decimal multiplier, int sortOrder);
}
