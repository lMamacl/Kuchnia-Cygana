using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

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
        const string sql = """
            SELECT
                (SELECT COUNT(1)
                 FROM [Recipes]
                 WHERE [IngredientId] = @IngredientId
                   AND [IsDeleted] = 0)
              + (SELECT COUNT(1)
                 FROM [RecipeComponentIngredients]
                 WHERE [IngredientId] = @IngredientId
                   AND [IsDeleted] = 0);
            """;
        var count = await db.ExecuteScalarAsync<int>(sql, new { IngredientId = ingredientId });
        return count == 0;
    }
}
