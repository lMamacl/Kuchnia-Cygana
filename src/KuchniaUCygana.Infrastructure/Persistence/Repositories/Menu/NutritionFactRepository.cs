using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class NutritionFactRepository : BaseRepository<NutritionFact>, INutritionFactRepository
{
    public NutritionFactRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<NutritionFact?> GetForMealAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [NutritionFacts] WHERE [MealId] = @MealId;";
        return await db.QuerySingleOrDefaultAsync<NutritionFact>(sql, new { MealId = mealId });
    }

    public async Task<NutritionFact?> GetForIngredientAsync(int ingredientId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [NutritionFacts] WHERE [IngredientId] = @IngredientId;";
        return await db.QuerySingleOrDefaultAsync<NutritionFact>(sql, new { IngredientId = ingredientId });
    }
}
