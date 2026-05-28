using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class IngredientManagementService : IIngredientManagementService
{
    private readonly IIngredientRepository ingredientRepository;
    private readonly IIngredientDeletionGuard deletionGuard;
    private readonly IMapper mapper;

    public IngredientManagementService(
        IIngredientRepository ingredientRepository,
        IIngredientDeletionGuard deletionGuard,
        IMapper mapper)
    {
        this.ingredientRepository = ingredientRepository;
        this.deletionGuard = deletionGuard;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<IngredientDto>> GetAllAsync()
    {
        var ingredients = await this.ingredientRepository.GetAllAsync();
        return this.mapper.Map<IEnumerable<IngredientDto>>(ingredients);
    }

    public async Task<IngredientDto?> GetAsync(int id)
    {
        var ingredient = await this.ingredientRepository.GetByIdAsync(id);
        return ingredient is null ? null : this.mapper.Map<IngredientDto>(ingredient);
    }

    public async Task<IngredientDto> CreateAsync(IngredientDto dto)
    {
        var ingredient = this.mapper.Map<Ingredient>(dto);
        await this.ingredientRepository.InsertAsync(ingredient);
        return this.mapper.Map<IngredientDto>(ingredient);
    }

    public async Task UpdateAsync(IngredientDto dto)
    {
        var ingredient = await this.ingredientRepository.GetByIdAsync(dto.Id);
        if (ingredient is null) return;
        this.mapper.Map(dto, ingredient);
        await this.ingredientRepository.UpdateAsync(ingredient);
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
}
