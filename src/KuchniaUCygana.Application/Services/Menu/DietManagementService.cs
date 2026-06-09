using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class DietManagementService : IDietManagementService
{
    private readonly IDietRepository dietRepository;
    private readonly IDietVariantRepository dietVariantRepository;
    private readonly IDietVariantMealRepository dietVariantMealRepository;
    private readonly IMapper mapper;

    public DietManagementService(
        IDietRepository dietRepository,
        IDietVariantRepository dietVariantRepository,
        IDietVariantMealRepository dietVariantMealRepository,
        IMapper mapper)
    {
        this.dietRepository = dietRepository;
        this.dietVariantRepository = dietVariantRepository;
        this.dietVariantMealRepository = dietVariantMealRepository;
        this.mapper = mapper;
    }

    public async Task<DietDto?> GetDietAsync(int dietId)
    {
        var diet = await this.dietRepository.GetByIdAsync(dietId);
        if (diet is null) return null;
        var dto = this.mapper.Map<DietDto>(diet);
        var variants = await this.dietVariantRepository.GetByDietIdAsync(dietId);
        dto.Variants = this.mapper.Map<List<DietVariantDto>>(variants);
        return dto;
    }

    public async Task<IEnumerable<DietDto>> GetActiveDietsAsync()
    {
        var rows = await this.dietRepository.GetActiveDietVariantRowsAsync();
        return rows
            .GroupBy(row => new
            {
                row.DietId,
                row.DietName,
                row.Description,
                row.MarketingDescription,
                row.Status,
                row.IsActive,
                row.ThumbnailUrl,
            })
            .Select(group => new DietDto
            {
                Id = group.Key.DietId,
                Name = group.Key.DietName,
                Description = group.Key.Description,
                MarketingDescription = group.Key.MarketingDescription,
                Status = group.Key.Status,
                IsActive = group.Key.IsActive,
                ThumbnailUrl = group.Key.ThumbnailUrl,
                Variants = group
                    .Where(row => row.DietVariantId.HasValue)
                    .Select(row => new DietVariantDto
                    {
                        Id = row.DietVariantId!.Value,
                        DietId = group.Key.DietId,
                        Name = row.VariantName ?? string.Empty,
                        TargetCalories = row.TargetCalories,
                        PriceMultiplier = row.PriceMultiplier,
                        IsDefault = row.IsDefault,
                    })
                    .ToList(),
            })
            .ToList();
    }

    public async Task<DietDto> CreateDietAsync(CreateDietRequest request)
    {
        var diet = new Diet { Name = request.Name, Description = request.Description };
        diet.Id = await this.dietRepository.InsertAsync(diet);
        foreach (var v in request.Variants)
        {
            var variant = new DietVariant
            {
                DietId = diet.Id,
                Name = v.Name,
                TargetCalories = v.TargetCalories,
                PriceMultiplier = v.PriceMultiplier,
                IsDefault = v.IsDefault
            };
            variant.Id = await this.dietVariantRepository.InsertAsync(variant);
        }
        return await this.GetDietAsync(diet.Id) ?? throw new InvalidOperationException();
    }

    public async Task UpdateDietAsync(int dietId, UpdateDietRequest request)
    {
        var diet = await this.dietRepository.GetByIdAsync(dietId);
        if (diet is null) return;
        this.mapper.Map(request, diet);
        await this.dietRepository.UpdateAsync(diet);
    }

    public async Task AddVariantAsync(int dietId, CreateDietVariantRequest request)
    {
        var variant = this.mapper.Map<DietVariant>(request);
        variant.DietId = dietId;
        variant.Id = await this.dietVariantRepository.InsertAsync(variant);
    }

    public async Task AssignMealToVariantAsync(int dietId, int variantId, int mealId, decimal multiplier, int sortOrder)
    {
        if (dietId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dietId), "Dieta jest wymagana.");
        }

        if (variantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(variantId), "Wariant diety jest wymagany.");
        }

        if (mealId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mealId), "Posiłek jest wymagany.");
        }

        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Mnożnik porcji musi być większy od zera.");
        }

        var variant = await this.dietVariantRepository.GetByIdAsync(variantId);
        if (variant is null)
        {
            throw new InvalidOperationException($"Wariant diety #{variantId} nie istnieje.");
        }

        if (variant.DietId != dietId)
        {
            throw new InvalidOperationException("Wariant diety nie nalezy do wskazanej diety.");
        }

        await this.dietVariantMealRepository.UpsertAsync(new DietVariantMeal
        {
            DietVariantId = variantId,
            MealId = mealId,
            ServingSizeMultiplier = multiplier,
            SortOrder = sortOrder,
        });
    }
}
