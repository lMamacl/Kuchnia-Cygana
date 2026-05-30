using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Application.Services;

public sealed class HaccpLocationService : IHaccpLocationService
{
    private readonly IHaccpLocationRepository _locationRepository;
    private readonly IWarehouseCategoryRepository _categoryRepository;

    public HaccpLocationService(
        IHaccpLocationRepository locationRepository,
        IWarehouseCategoryRepository categoryRepository)
    {
        _locationRepository = locationRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<HaccpLocationDto>> GetActiveAsync()
    {
        var rows = await _locationRepository.GetAllWithCategoriesAsync(activeOnly: true);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<HaccpLocationDto>> GetAllAsync()
    {
        var rows = await _locationRepository.GetAllWithCategoriesAsync(activeOnly: false);
        return rows.Select(Map).ToList();
    }

    public async Task<HaccpLocationDto?> GetByIdAsync(int id)
    {
        var row = await _locationRepository.GetDetailsByIdAsync(id);
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(SaveHaccpLocationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new InvalidOperationException("Kod lokalizacji jest wymagany.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Nazwa lokalizacji jest wymagana.");
        }

        if (request.MinTemperatureCelsius > request.MaxTemperatureCelsius)
        {
            throw new InvalidOperationException("Minimalna temperatura nie może być większa niż maksymalna.");
        }

        var activeCategories = await _categoryRepository.GetActiveOrderedAsync();
        var activeIds = activeCategories.Select(c => c.Id).ToHashSet();
        var selectedCategoryIds = request.CategoryIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (selectedCategoryIds.Any(id => !activeIds.Contains(id)))
        {
            throw new InvalidOperationException("Wybrano nieaktywną lub nieistniejącą kategorię magazynową.");
        }

        var entity = new HaccpLocation
        {
            Id = request.Id,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            MinTemperatureCelsius = request.MinTemperatureCelsius,
            MaxTemperatureCelsius = request.MaxTemperatureCelsius,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        if (entity.Id > 0)
        {
            await _locationRepository.UpdateAsync(entity);
        }
        else
        {
            entity.Id = await _locationRepository.InsertAsync(entity);
        }

        await _locationRepository.ReplaceCategoriesAsync(entity.Id, selectedCategoryIds);
    }

    private static HaccpLocationDto Map(HaccpLocationDetailsRow row)
    {
        return new HaccpLocationDto
        {
            Id = row.Id,
            Code = row.Code,
            Name = row.Name,
            MinTemperatureCelsius = row.MinTemperatureCelsius,
            MaxTemperatureCelsius = row.MaxTemperatureCelsius,
            IsActive = row.IsActive,
            DisplayOrder = row.DisplayOrder,
            Notes = row.Notes,
            CategoryIds = ParseIds(row.CategoryIds),
            CategoryNames = row.CategoryNames,
        };
    }

    private static IReadOnlyList<int> ParseIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<int>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }
}
