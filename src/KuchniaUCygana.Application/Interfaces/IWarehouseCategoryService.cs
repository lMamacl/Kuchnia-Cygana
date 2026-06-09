using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces;

public interface IWarehouseCategoryService
{
    Task<IReadOnlyList<WarehouseCategoryDto>> GetActiveAsync();

    Task<IReadOnlyList<WarehouseCategoryDto>> GetAllAsync();
}
