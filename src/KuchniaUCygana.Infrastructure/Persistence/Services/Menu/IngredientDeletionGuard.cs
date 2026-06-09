using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Infrastructure.Persistence.Services.Menu;

public sealed class IngredientDeletionGuard : IIngredientDeletionGuard
{
    private readonly IIngredientRepository ingredientRepository;

    public IngredientDeletionGuard(IIngredientRepository ingredientRepository)
    {
        this.ingredientRepository = ingredientRepository;
    }

    public async Task<bool> CanDeleteAsync(int ingredientId)
    {
        return await this.ingredientRepository.CanDeleteAsync(ingredientId);
    }
}
