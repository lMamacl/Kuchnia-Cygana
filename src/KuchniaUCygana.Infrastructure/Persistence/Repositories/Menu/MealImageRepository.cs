using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class MealImageRepository : BaseRepository<MealImage>, IMealImageRepository
{
    public MealImageRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<MealImage>> GetByMealIdAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        return await db.SelectAsync<MealImage>(mi => mi.MealId == mealId);
    }

    public async Task<MealImage?> GetMainForMealAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        return await db.SingleAsync<MealImage>(mi => mi.MealId == mealId && mi.IsMain);
    }

    public async Task<bool> DeleteByMealAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        return await db.DeleteAsync<MealImage>(mi => mi.MealId == mealId) > 0;
    }
}
