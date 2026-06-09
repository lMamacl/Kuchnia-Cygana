using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Infrastructure.Persistence.Services.Menu;

public sealed class AllergenPropagationService : IAllergenPropagationService
{
    private readonly IMealAllergenRepository mealAllergenRepository;

    public AllergenPropagationService(IMealAllergenRepository mealAllergenRepository)
    {
        this.mealAllergenRepository = mealAllergenRepository;
    }

    public async Task PropagateAllergenAsync(int mealId)
    {
        await this.mealAllergenRepository.RecalculateForMealAsync(mealId);
    }
}
