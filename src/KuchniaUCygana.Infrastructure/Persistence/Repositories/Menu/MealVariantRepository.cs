using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class MealVariantRepository : IMealVariantRepository
{
    private readonly IDbConnectionFactory factory;

    public MealVariantRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IReadOnlyList<MealVariantRow>> GetByMealIdAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<MealVariantRow>(
            """
            SELECT *
            FROM [MealVariants]
            WHERE [MealId] = @mealId
              AND [IsDeleted] = 0
            ORDER BY [IsDefault] DESC, [Name], [Id];
            """,
            new { mealId });

        return rows.ToList();
    }

    public async Task<MealVariantRow?> GetByIdAsync(int mealVariantId)
    {
        using var db = this.factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<MealVariantRow>(
            """
            SELECT *
            FROM [MealVariants]
            WHERE [Id] = @mealVariantId
              AND [IsDeleted] = 0;
            """,
            new { mealVariantId });
    }

    public async Task<MealVariantRow?> GetDefaultByMealIdAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<MealVariantRow>(
            """
            SELECT TOP 1 *
            FROM [MealVariants]
            WHERE [MealId] = @mealId
              AND [IsDefault] = 1
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { mealId });
    }

    public async Task<int> CreateAsync(MealVariant variant, int? sourceMealVariantId, string? userName)
    {
        using var db = this.factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        var variantId = await db.QuerySingleAsync<int>(
            """
            INSERT INTO [MealVariants]
                ([MealId], [Name], [VariantType], [Status], [Description], [IsDefault],
                 [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                 [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [NutritionSource],
                 [NutritionOverrideReason], [AllergensApproved], [AllergenOverrideReason],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@MealId, @Name, @VariantType, @Status, @Description, @IsDefault,
                 @RawWeightGrams, @CookedWeightGrams, @CaloriesPer100g, @ProteinPer100g,
                 @CarbohydratesPer100g, @FatPer100g, @FiberPer100g, @NutritionSource,
                 @NutritionOverrideReason, @AllergensApproved, @AllergenOverrideReason,
                 @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            variant,
            tx);

        if (sourceMealVariantId.HasValue)
        {
            await db.ExecuteAsync(
                """
                INSERT INTO [MealVariantComponents]
                    ([MealVariantId], [RecipeComponentVersionId], [Role], [QuantityPerServing],
                     [Unit], [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    @variantId,
                    [RecipeComponentVersionId],
                    [Role],
                    [QuantityPerServing],
                    [Unit],
                    [SortOrder],
                    [IsOptional],
                    @createdAt,
                    @userName,
                    0
                FROM [MealVariantComponents]
                WHERE [MealVariantId] = @sourceMealVariantId
                  AND [IsDeleted] = 0;
                """,
                new
                {
                    variantId,
                    sourceMealVariantId,
                    createdAt = variant.CreatedAt,
                    userName,
                },
                tx);
        }

        tx.Commit();
        return variantId;
    }

    public async Task<IReadOnlyList<MealVariantComponentRow>> GetComponentsAsync(int mealVariantId)
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<MealVariantComponentRow>(
            """
            SELECT
                mvc.[Id],
                mvc.[MealVariantId],
                rc.[Id] AS [RecipeComponentId],
                rcv.[Id] AS [RecipeComponentVersionId],
                rc.[Name] AS [ComponentName],
                rcv.[VersionNumber],
                rcv.[Status] AS [VersionStatus],
                mvc.[Role],
                mvc.[QuantityPerServing],
                mvc.[Unit],
                mvc.[SortOrder],
                mvc.[IsOptional]
            FROM [MealVariantComponents] mvc
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mvc.[RecipeComponentVersionId]
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            WHERE mvc.[MealVariantId] = @mealVariantId
              AND mvc.[IsDeleted] = 0
              AND rcv.[IsDeleted] = 0
              AND rc.[IsDeleted] = 0
            ORDER BY mvc.[SortOrder], mvc.[Id];
            """,
            new { mealVariantId });

        return rows.ToList();
    }
}
