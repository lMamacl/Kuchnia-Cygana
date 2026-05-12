using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class RecipeRepository : IRecipeRepository
{
    private readonly IDbConnectionFactory factory;

    public RecipeRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IEnumerable<Recipe>> GetByMealIdAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        return await db.SelectAsync<Recipe>(r => r.MealId == mealId);
    }

    public async Task<int> InsertAsync(Recipe recipe)
    {
        using var db = this.factory.CreateConnection();
        return (int)await db.InsertAsync(recipe, selectIdentity: true);
    }

    public async Task<bool> UpdateAsync(Recipe recipe)
    {
        using var db = this.factory.CreateConnection();
        return await db.UpdateAsync(recipe) > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var db = this.factory.CreateConnection();
        return await db.DeleteByIdAsync<Recipe>(id) > 0;
    }

    public async Task<bool> DeleteByMealAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        return await db.DeleteAsync<Recipe>(r => r.MealId == mealId) > 0;
    }
}
