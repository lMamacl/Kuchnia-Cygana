using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class MealAllergenRepository : IMealAllergenRepository
{
    private readonly IDbConnectionFactory factory;

    public MealAllergenRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IEnumerable<MealAllergen>> GetByMealIdAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        const string sql = "SELECT * FROM [MealAllergens] WHERE [MealId] = @MealId;";
        return await db.QueryAsync<MealAllergen>(sql, new { MealId = mealId });
    }

    public async Task<IReadOnlyList<MealAllergenRow>> GetDetailsByMealIdAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<MealAllergenRow>(
            """
            SELECT
                a.[Id] AS [AllergenId],
                a.[Name],
                ma.[IsTrace]
            FROM [MealAllergens] ma
            INNER JOIN [Allergens] a ON a.[Id] = ma.[AllergenId]
            WHERE ma.[MealId] = @mealId
            ORDER BY a.[Name];
            """,
            new { mealId });

        return rows.ToList();
    }

    public async Task RecalculateForMealAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();

        // 1. Usuń stare wpisy (DELETE)
        const string deleteSql = "DELETE FROM [MealAllergens] WHERE [MealId] = @MealId;";
        await db.ExecuteAsync(deleteSql, new { MealId = mealId });

        // 2. Pobierz ID składników z przepisu
        const string recipeSql = "SELECT [IngredientId] FROM [Recipes] WHERE [MealId] = @MealId;";
        var ingredientIds = (await db.QueryAsync<int>(recipeSql, new { MealId = mealId })).ToList();
        if (!ingredientIds.Any()) return;

        // 3. Pobierz powiązania składnik-alergen
        const string allergenSql = @"
            SELECT [AllergenId], [TraceAmount] 
            FROM [IngredientAllergens] 
            WHERE [IngredientId] IN @Ids;";
        var ingredientAllergens = await db.QueryAsync<(int AllergenId, bool TraceAmount)>(
            allergenSql, new { Ids = ingredientIds });

        if (!ingredientAllergens.Any()) return;

        // 4. Agreguj alergeny
        var allergenGroups = ingredientAllergens
            .GroupBy(ia => ia.AllergenId)
            .Select(g => new MealAllergen
            {
                MealId = mealId,
                AllergenId = g.Key,
                IsTrace = g.All(ia => ia.TraceAmount)
            });

        // 5. Wstaw nowe wpisy
        const string insertSql = @"
            INSERT INTO [MealAllergens] ([MealId], [AllergenId], [IsTrace]) 
            VALUES (@MealId, @AllergenId, @IsTrace);";
        foreach (var ma in allergenGroups)
        {
            await db.ExecuteAsync(insertSql, new { ma.MealId, ma.AllergenId, ma.IsTrace });
        }
    }

    public async Task<bool> DeleteByMealAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        const string sql = "DELETE FROM [MealAllergens] WHERE [MealId] = @MealId;";
        var rows = await db.ExecuteAsync(sql, new { MealId = mealId });
        return rows > 0;
    }
}
