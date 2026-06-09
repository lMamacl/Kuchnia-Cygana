using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class AllergenManagementService : IAllergenManagementService
{
    private readonly IAllergenRepository allergenRepository;
    private readonly IMapper mapper;

    public AllergenManagementService(IAllergenRepository allergenRepository, IMapper mapper)
    {
        this.allergenRepository = allergenRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<AllergenDto>> GetAllAsync()
    {
        var allergens = await this.allergenRepository.GetAllOrderedAsync();
        return this.mapper.Map<IEnumerable<AllergenDto>>(allergens);
    }

    public async Task<AllergenDto?> GetAsync(int id)
    {
        var allergen = await this.allergenRepository.GetByIdAsync(id);
        return allergen is null ? null : this.mapper.Map<AllergenDto>(allergen);
    }

    public async Task<AllergenDto> CreateAsync(AllergenDto dto)
    {
        var allergen = this.mapper.Map<Allergen>(dto);
        await this.allergenRepository.InsertAsync(allergen);
        return this.mapper.Map<AllergenDto>(allergen);
    }

    public async Task UpdateAsync(AllergenDto dto)
    {
        var allergen = await this.allergenRepository.GetByIdAsync(dto.Id);
        if (allergen is null) return;
        this.mapper.Map(dto, allergen);
        await this.allergenRepository.UpdateAsync(allergen);
    }

    public async Task DeleteAsync(int id)
    {
        await this.allergenRepository.DeleteAsync(id);
    }
}
