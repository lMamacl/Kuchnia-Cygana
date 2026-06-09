using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Application.Services;

public sealed class WarehouseCategoryService : IWarehouseCategoryService
{
    private readonly IWarehouseCategoryRepository _categoryRepository;

    public WarehouseCategoryService(IWarehouseCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<WarehouseCategoryDto>> GetActiveAsync()
    {
        var categories = await _categoryRepository.GetActiveOrderedAsync();
        return categories.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<WarehouseCategoryDto>> GetAllAsync()
    {
        var categories = await _categoryRepository.GetAllOrderedAsync();
        return categories.Select(Map).ToList();
    }

    private static WarehouseCategoryDto Map(WarehouseCategory category)
    {
        return new WarehouseCategoryDto
        {
            Id = category.Id,
            Code = category.Code,
            Name = category.Name,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder,
        };
    }
}
