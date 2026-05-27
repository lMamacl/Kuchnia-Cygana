using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

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
        const string sql = "SELECT * FROM [Meals] WHERE [Status] = @Status AND [IsDeleted] = 0;";
        return await db.QueryAsync<Meal>(sql, new { Status = MealStatus.Published.ToString() });
    }

    public async Task<Meal?> GetWithRecipeAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        const string mealSql = "SELECT * FROM [Meals] WHERE [Id] = @Id AND [IsDeleted] = 0;";
        var meal = await db.QuerySingleOrDefaultAsync<Meal>(mealSql, new { Id = mealId });
        if (meal is null) return null;
        // recipes nie są używane w tej metodzie poza sprawdzeniem – można pominąć lub załadować osobno
        return meal;
    }
}
