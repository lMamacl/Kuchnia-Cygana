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
    private readonly IMapper mapper;

    public DietManagementService(
        IDietRepository dietRepository,
        IDietVariantRepository dietVariantRepository,
        IMapper mapper)
    {
        this.dietRepository = dietRepository;
        this.dietVariantRepository = dietVariantRepository;
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
        var diets = await this.dietRepository.GetActiveWithVariantsAsync();
        var result = new List<DietDto>();
        foreach (var diet in diets)
        {
            var dto = this.mapper.Map<DietDto>(diet);
            var variants = await this.dietVariantRepository.GetByDietIdAsync(diet.Id);
            dto.Variants = this.mapper.Map<List<DietVariantDto>>(variants);
            result.Add(dto);
        }
        return result;
    }

    public async Task<DietDto> CreateDietAsync(CreateDietRequest request)
    {
        var diet = new Diet { Name = request.Name, Description = request.Description };
        await this.dietRepository.InsertAsync(diet);
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
            await this.dietVariantRepository.InsertAsync(variant);
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
        await this.dietVariantRepository.InsertAsync(variant);
    }

    public async Task AssignMealToVariantAsync(int variantId, int mealId, decimal multiplier, int sortOrder)
    {
        // Implementacja wymaga repozytorium DietVariantMeal, które nie ma interfejsu, więc użyjemy IDbConnectionFactory.
        // Dla uproszczenia pomijam – do pełnej implementacji w Etapie G/H.
        await Task.CompletedTask;
    }
}
