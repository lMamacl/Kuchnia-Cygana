using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class MealRepository : BaseRepository<Meal>, IMealRepository
{
    public MealRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<Meal>> GetPublishedAsync()
    {
        using var db = this.Factory.CreateConnection();
        return await db.SelectAsync<Meal>(m =>
            m.Status == MealStatus.Published && !m.IsDeleted);
    }

    public async Task<Meal?> GetWithRecipeAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        var meal = await db.SingleByIdAsync<Meal>(mealId);

        if (meal is null || meal.IsDeleted)
        {
            return null;
        }

        var recipes = await db.SelectAsync<Recipe>(r => r.MealId == mealId);
        return meal;
    }
}
