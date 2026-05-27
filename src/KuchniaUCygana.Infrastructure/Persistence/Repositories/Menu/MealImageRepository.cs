using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

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
        const string sql = "SELECT * FROM [MealImages] WHERE [MealId] = @MealId;";
        return await db.QueryAsync<MealImage>(sql, new { MealId = mealId });
    }

    public async Task<MealImage?> GetMainForMealAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT TOP 1 * FROM [MealImages] WHERE [MealId] = @MealId AND [IsMain] = 1;";
        return await db.QuerySingleOrDefaultAsync<MealImage>(sql, new { MealId = mealId });
    }

    public async Task<bool> DeleteByMealAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "DELETE FROM [MealImages] WHERE [MealId] = @MealId;";
        var rows = await db.ExecuteAsync(sql, new { MealId = mealId });
        return rows > 0;
    }
}
