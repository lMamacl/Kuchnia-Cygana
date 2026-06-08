using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class IngredientManagementService : IIngredientManagementService
{
    private readonly IIngredientRepository ingredientRepository;
    private readonly INutritionFactRepository nutritionFactRepository;
    private readonly IIngredientDeletionGuard deletionGuard;
    private readonly IMapper mapper;

    public IngredientManagementService(
        IIngredientRepository ingredientRepository,
        INutritionFactRepository nutritionFactRepository,
        IIngredientDeletionGuard deletionGuard,
        IMapper mapper)
    {
        this.ingredientRepository = ingredientRepository;
        this.nutritionFactRepository = nutritionFactRepository;
        this.deletionGuard = deletionGuard;
        this.mapper = mapper;
    }

    public async Task<PagedResultDto<IngredientListItemDto>> SearchAsync(IngredientSearchFilterDto filter)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 100);

        var result = await this.ingredientRepository.SearchAsync(new IngredientSearchQuery
        {
            Search = Normalize(filter.Search),
            ResourceType = Normalize(filter.ResourceType),
            FoodCategoryId = filter.FoodCategoryId,
            WarehouseCategoryId = filter.WarehouseCategoryId,
            AllergenId = filter.AllergenId,
            MissingWarehouseMapping = filter.MissingWarehouseMapping,
            IsActive = filter.IsActive,
            Page = page,
            PageSize = pageSize,
        });

        filter.Page = page;
        filter.PageSize = pageSize;

        return new PagedResultDto<IngredientListItemDto>
        {
            Items = result.Items.Select(MapListItem).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<IReadOnlyList<IngredientListItemDto>> SearchRecipeLookupAsync(
        string? query,
        int page = 1,
        int pageSize = 20)
    {
        page = page <= 0 ? 1 : page;
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 20);
        var rows = await this.ingredientRepository.SearchRecipeLookupAsync(Normalize(query), page, pageSize);
        return rows.Select(MapListItem).ToList();
    }

    public async Task<IEnumerable<IngredientDto>> GetAllAsync()
    {
        var ingredients = await this.ingredientRepository.GetAllAsync();
        return this.mapper.Map<IEnumerable<IngredientDto>>(ingredients);
    }

    public async Task<IngredientDto?> GetAsync(int id)
    {
        var ingredient = await this.ingredientRepository.GetByIdAsync(id);
        if (ingredient is null)
        {
            return null;
        }

        var dto = this.mapper.Map<IngredientDto>(ingredient);
        await this.EnrichAsync(dto);
        return dto;
    }

    public async Task<IngredientDto> CreateAsync(IngredientDto dto)
    {
        var ingredient = this.mapper.Map<Ingredient>(dto);
        ingredient.Id = await this.ingredientRepository.InsertAsync(ingredient);

        await this.SaveNutritionAsync(ingredient.Id, dto.Nutrition);
        await this.SaveAllergensAsync(ingredient.Id, dto.SelectedAllergenIds, dto.TraceAllergenIds);

        return await this.GetAsync(ingredient.Id)
            ?? this.mapper.Map<IngredientDto>(ingredient);
    }

    public async Task UpdateAsync(IngredientDto dto)
    {
        var ingredient = await this.ingredientRepository.GetByIdAsync(dto.Id);
        if (ingredient is null)
        {
            return;
        }

        this.mapper.Map(dto, ingredient);
        await this.ingredientRepository.UpdateAsync(ingredient);
        await this.SaveNutritionAsync(dto.Id, dto.Nutrition);
        await this.SaveAllergensAsync(dto.Id, dto.SelectedAllergenIds, dto.TraceAllergenIds);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (await this.deletionGuard.CanDeleteAsync(id))
        {
            await this.ingredientRepository.DeleteAsync(id);
            return true;
        }

        return false;
    }

    private async Task EnrichAsync(IngredientDto dto)
    {
        var nutrition = await this.nutritionFactRepository.GetForIngredientAsync(dto.Id);
        dto.Nutrition = nutrition is null
            ? new NutritionFactDto()
            : this.mapper.Map<NutritionFactDto>(nutrition);

        var allergens = await this.ingredientRepository.GetIngredientAllergensAsync(dto.Id);
        dto.Allergens = allergens
            .Select(row => new IngredientAllergenDto
            {
                AllergenId = row.AllergenId,
                Name = row.Name,
                Code = row.Code,
                IconUrl = row.IconUrl,
                TraceAmount = row.TraceAmount,
            })
            .ToList();
        dto.SelectedAllergenIds = dto.Allergens.Select(a => a.AllergenId).Distinct().ToList();
        dto.TraceAllergenIds = dto.Allergens.Where(a => a.TraceAmount).Select(a => a.AllergenId).Distinct().ToList();
    }

    private async Task SaveNutritionAsync(int ingredientId, NutritionFactDto? dto)
    {
        var existing = await this.nutritionFactRepository.GetForIngredientAsync(ingredientId);
        if (existing is null && !HasNutritionValues(dto))
        {
            return;
        }

        if (existing is null)
        {
            var nutrition = MapNutrition(ingredientId, dto!);
            await this.nutritionFactRepository.InsertAsync(nutrition);
            return;
        }

        existing.MealId = null;
        existing.IngredientId = ingredientId;
        existing.CaloriesPer100g = dto?.CaloriesPer100g ?? 0m;
        existing.ProteinPer100g = dto?.ProteinPer100g ?? 0m;
        existing.CarbohydratesPer100g = dto?.CarbohydratesPer100g ?? 0m;
        existing.FatPer100g = dto?.FatPer100g ?? 0m;
        existing.FiberPer100g = dto?.FiberPer100g ?? 0m;
        await this.nutritionFactRepository.UpdateAsync(existing);
    }

    private async Task SaveAllergensAsync(
        int ingredientId,
        IEnumerable<int>? selectedAllergenIds,
        IEnumerable<int>? traceAllergenIds)
    {
        var traces = (traceAllergenIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .ToHashSet();

        var allergens = (selectedAllergenIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .Select(id => new IngredientAllergen
            {
                IngredientId = ingredientId,
                AllergenId = id,
                TraceAmount = traces.Contains(id),
            })
            .ToList();

        await this.ingredientRepository.SaveIngredientAllergensAsync(ingredientId, allergens);
    }

    private static NutritionFact MapNutrition(int ingredientId, NutritionFactDto dto)
    {
        return new NutritionFact
        {
            IngredientId = ingredientId,
            CaloriesPer100g = dto.CaloriesPer100g,
            ProteinPer100g = dto.ProteinPer100g,
            CarbohydratesPer100g = dto.CarbohydratesPer100g,
            FatPer100g = dto.FatPer100g,
            FiberPer100g = dto.FiberPer100g,
        };
    }

    private static bool HasNutritionValues(NutritionFactDto? dto)
    {
        return dto is not null
            && (dto.Id > 0
                || dto.CaloriesPer100g != 0m
                || dto.ProteinPer100g != 0m
                || dto.CarbohydratesPer100g != 0m
                || dto.FatPer100g != 0m
                || dto.FiberPer100g != 0m);
    }

    private static IngredientListItemDto MapListItem(IngredientListRow row)
    {
        return new IngredientListItemDto
        {
            Id = row.Id,
            Name = row.Name,
            ResourceType = row.ResourceType,
            FoodCategoryId = row.FoodCategoryId,
            FoodCategoryName = row.FoodCategoryName,
            Unit = row.Unit,
            CostPerUnit = row.CostPerUnit,
            Notes = row.Notes,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            MissingWarehouseMapping = row.MissingWarehouseMapping,
            IsActive = row.IsActive,
            CaloriesPer100g = row.CaloriesPer100g,
            ProteinPer100g = row.ProteinPer100g,
            CarbohydratesPer100g = row.CarbohydratesPer100g,
            FatPer100g = row.FatPer100g,
            FiberPer100g = row.FiberPer100g,
            AllergenNames = row.AllergenNames,
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
