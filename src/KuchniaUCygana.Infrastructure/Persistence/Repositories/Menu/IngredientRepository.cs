using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class IngredientRepository : BaseRepository<Ingredient>, IIngredientRepository
{
    public IngredientRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<bool> CanDeleteAsync(int ingredientId)
    {
        using var db = this.Factory.CreateConnection();
        return await db.CountAsync<Recipe>(r => r.IngredientId == ingredientId) == 0;
    }
}
