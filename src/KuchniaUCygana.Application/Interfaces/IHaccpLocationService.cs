using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces;

public interface IHaccpLocationService
{
    Task<IReadOnlyList<HaccpLocationDto>> GetActiveAsync();

    Task<IReadOnlyList<HaccpLocationDto>> GetAllAsync();

    Task<HaccpLocationDto?> GetByIdAsync(int id);

    Task SaveAsync(SaveHaccpLocationRequest request);
}
