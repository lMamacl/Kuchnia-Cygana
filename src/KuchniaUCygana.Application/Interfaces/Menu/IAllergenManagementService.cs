using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IAllergenManagementService
{
    Task<IEnumerable<AllergenDto>> GetAllAsync();

    Task<AllergenDto?> GetAsync(int id);

    Task<AllergenDto> CreateAsync(AllergenDto dto);

    Task UpdateAsync(AllergenDto dto);

    Task DeleteAsync(int id);
}
