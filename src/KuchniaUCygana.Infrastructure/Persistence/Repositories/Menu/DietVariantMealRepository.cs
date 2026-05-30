using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietVariantMealRepository : IDietVariantMealRepository
{
    private readonly IDbConnectionFactory factory;

    public DietVariantMealRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<DietVariantMeal?> GetAsync(int variantId, int mealId)
    {
        using var db = this.factory.CreateConnection();
        const string sql = """
            SELECT TOP 1 *
            FROM [DietVariantMeals]
            WHERE [DietVariantId] = @variantId
              AND [MealId] = @mealId;
            """;

        return await db.QuerySingleOrDefaultAsync<DietVariantMeal>(sql, new { variantId, mealId });
    }

    public async Task<IEnumerable<DietVariantMeal>> GetByVariantIdAsync(int variantId)
    {
        using var db = this.factory.CreateConnection();
        const string sql = """
            SELECT *
            FROM [DietVariantMeals]
            WHERE [DietVariantId] = @variantId
            ORDER BY [SortOrder], [Id];
            """;

        return await db.QueryAsync<DietVariantMeal>(sql, new { variantId });
    }

    public async Task UpsertAsync(DietVariantMeal assignment)
    {
        using var db = this.factory.CreateConnection();
        const string sql = """
            IF EXISTS (
                SELECT 1
                FROM [DietVariantMeals]
                WHERE [DietVariantId] = @DietVariantId
                  AND [MealId] = @MealId
            )
            BEGIN
                UPDATE [DietVariantMeals]
                SET [ServingSizeMultiplier] = @ServingSizeMultiplier,
                    [SortOrder] = @SortOrder
                WHERE [DietVariantId] = @DietVariantId
                  AND [MealId] = @MealId;
            END
            ELSE
            BEGIN
                INSERT INTO [DietVariantMeals] ([DietVariantId], [MealId], [ServingSizeMultiplier], [SortOrder])
                VALUES (@DietVariantId, @MealId, @ServingSizeMultiplier, @SortOrder);
            END
            """;

        await db.ExecuteAsync(sql, assignment);
    }
}
