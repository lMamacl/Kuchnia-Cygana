using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

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
        const string sql = "SELECT * FROM [Recipes] WHERE [MealId] = @MealId;";
        return await db.QueryAsync<Recipe>(sql, new { MealId = mealId });
    }

    public async Task<int> InsertAsync(Recipe recipe)
    {
        using var db = this.factory.CreateConnection();
        const string sql = @"
            INSERT INTO [Recipes] ([MealId], [IngredientId], [WeightInGrams], [IsOptional], [Notes])
            VALUES (@MealId, @IngredientId, @WeightInGrams, @IsOptional, @Notes);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await db.QuerySingleAsync<int>(sql, recipe);
    }

    public async Task<bool> UpdateAsync(Recipe recipe)
    {
        using var db = this.factory.CreateConnection();
        const string sql = @"
            UPDATE [Recipes] SET
                [MealId] = @MealId,
                [IngredientId] = @IngredientId,
                [WeightInGrams] = @WeightInGrams,
                [IsOptional] = @IsOptional,
                [Notes] = @Notes
            WHERE [Id] = @Id;";
        int rowsAffected = await db.ExecuteAsync(sql, recipe);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var db = this.factory.CreateConnection();
        const string sql = "DELETE FROM [Recipes] WHERE [Id] = @Id;";
        int rowsAffected = await db.ExecuteAsync(sql, new { Id = id });
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteByMealAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        const string sql = "DELETE FROM [Recipes] WHERE [MealId] = @MealId;";
        int rowsAffected = await db.ExecuteAsync(sql, new { MealId = mealId });
        return rowsAffected > 0;
    }
}
