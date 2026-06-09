using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IIngredientManagementService
{
    Task<PagedResultDto<IngredientListItemDto>> SearchAsync(IngredientSearchFilterDto filter);

    Task<IReadOnlyList<IngredientListItemDto>> SearchRecipeLookupAsync(string? query, int page = 1, int pageSize = 20);

    Task<IEnumerable<IngredientDto>> GetAllAsync();

    Task<IngredientDto?> GetAsync(int id);

    Task<IngredientDto> CreateAsync(IngredientDto dto);

    Task UpdateAsync(IngredientDto dto);

    Task<bool> DeleteAsync(int id);
}
