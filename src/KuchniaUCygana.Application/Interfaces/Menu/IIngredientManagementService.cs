using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IIngredientManagementService
{
    Task<IEnumerable<IngredientDto>> GetAllAsync();

    Task<IngredientDto?> GetAsync(int id);

    Task<IngredientDto> CreateAsync(IngredientDto dto);

    Task UpdateAsync(IngredientDto dto);

    Task<bool> DeleteAsync(int id);
}
