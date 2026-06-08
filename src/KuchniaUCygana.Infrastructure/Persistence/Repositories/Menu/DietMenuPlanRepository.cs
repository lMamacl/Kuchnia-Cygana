using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietMenuPlanRepository : IDietMenuPlanRepository
{
    private readonly IDbConnectionFactory factory;

    public DietMenuPlanRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IReadOnlyList<DietMenuPlanDayRow>> GetPlansAsync(DateOnly startDate, DateOnly endDate)
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<DietMenuPlanDayRow>(
            """
            SELECT
                p.[Id],
                p.[PlanDate],
                p.[Status],
                p.[Notes],
                p.[PublishedAt],
                p.[PublishedBy],
                COUNT(i.[Id]) AS [ActiveItemCount]
            FROM [DietMenuPlans] p
            LEFT JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
                AND i.[IsDeleted] = 0
                AND i.[IsActive] = 1
            WHERE p.[IsDeleted] = 0
              AND p.[PlanDate] >= @startDate
              AND p.[PlanDate] <= @endDate
            GROUP BY p.[Id], p.[PlanDate], p.[Status], p.[Notes], p.[PublishedAt], p.[PublishedBy]
            ORDER BY p.[PlanDate];
            """,
            new { startDate, endDate });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<DietMenuPlanDaySummaryRow>> GetPlanSummariesAsync(DateOnly startDate, DateOnly endDate)
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<DietMenuPlanDaySummaryRow>(
            """
            SELECT
                p.[Id],
                p.[PlanDate],
                p.[Status],
                p.[PublishedAt],
                p.[PublishedBy],
                COUNT(i.[Id]) AS [ActiveItemCount],
                COALESCE(SUM(CASE
                    WHEN i.[Id] IS NULL THEN 0
                    WHEN m.[Id] IS NULL THEN 1
                    WHEN m.[Status] NOT IN (N'Published', N'Active') THEN 1
                    WHEN i.[MealVariantId] IS NOT NULL
                     AND (mv.[Id] IS NULL OR mv.[Status] NOT IN (N'Published', N'Active')) THEN 1
                    WHEN COALESCE(componentCounts.[ComponentCount], 0) = 0
                     AND COALESCE(recipeCounts.[LegacyRecipeCount], 0) = 0 THEN 1
                    ELSE 0
                END), 0) AS [QuickWarningCount]
            FROM [DietMenuPlans] p
            LEFT JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
                AND i.[IsDeleted] = 0
                AND i.[IsActive] = 1
            LEFT JOIN [Meals] m ON m.[Id] = i.[MealId]
                AND m.[IsDeleted] = 0
                AND m.[IsActive] = 1
            LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId]
                AND mv.[IsDeleted] = 0
            OUTER APPLY (
                SELECT COUNT(*) AS [ComponentCount]
                FROM (
                    SELECT mrc.[RecipeComponentVersionId]
                    FROM [MealRecipeComponents] mrc
                    WHERE i.[MealVariantId] IS NULL
                      AND mrc.[MealId] = i.[MealId]
                      AND mrc.[IsDeleted] = 0

                    UNION ALL

                    SELECT mvc.[RecipeComponentVersionId]
                    FROM [MealVariantComponents] mvc
                    WHERE mvc.[MealVariantId] = i.[MealVariantId]
                      AND mvc.[IsDeleted] = 0
                ) componentSource
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = componentSource.[RecipeComponentVersionId]
                WHERE rcv.[IsDeleted] = 0
            ) componentCounts
            OUTER APPLY (
                SELECT COUNT(*) AS [LegacyRecipeCount]
                FROM [Recipes] r
                WHERE r.[MealId] = i.[MealId]
                  AND r.[IsDeleted] = 0
            ) recipeCounts
            WHERE p.[IsDeleted] = 0
              AND p.[PlanDate] >= @startDate
              AND p.[PlanDate] <= @endDate
            GROUP BY p.[Id], p.[PlanDate], p.[Status], p.[PublishedAt], p.[PublishedBy]
            ORDER BY p.[PlanDate];
            """,
            new { startDate, endDate });

        return rows.ToList();
    }

    public async Task<DietMenuPlanDayRow?> GetPlanByDateAsync(DateOnly date)
    {
        using var db = this.factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<DietMenuPlanDayRow>(
            DaySql("p.[PlanDate] = @date"),
            new { date });
    }

    public async Task<DietMenuPlanDayRow?> GetPlanByIdAsync(int planId)
    {
        using var db = this.factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<DietMenuPlanDayRow>(
            DaySql("p.[Id] = @planId"),
            new { planId });
    }

    public async Task<IReadOnlyList<DietMenuPlanItemRow>> GetPlanItemsAsync(int planId)
    {
        return await this.GetPlanItemsAsync(planId, null);
    }

    public async Task<IReadOnlyList<DietMenuPlanItemRow>> GetPlanItemsAsync(int planId, int? dietVariantId)
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<DietMenuPlanItemRow>(
            ItemSql("i.[DietMenuPlanId] = @planId AND (@dietVariantId IS NULL OR i.[DietVariantId] = @dietVariantId)"),
            new { planId, dietVariantId });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<DietMenuPlanDietVariantSummaryRow>> GetPlanDietVariantSummariesAsync(int planId)
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<DietMenuPlanDietVariantSummaryRow>(
            """
            SELECT
                dv.[Id] AS [DietVariantId],
                d.[Name] AS [DietName],
                dv.[Name] AS [VariantName],
                dv.[TargetCalories],
                dv.[IsDefault],
                COUNT(i.[Id]) AS [ActiveItemCount],
                COALESCE(SUM(CASE
                    WHEN i.[Id] IS NULL THEN 0
                    WHEN m.[Id] IS NULL THEN 1
                    WHEN m.[Status] NOT IN (N'Published', N'Active') THEN 1
                    WHEN i.[MealVariantId] IS NOT NULL
                     AND (mv.[Id] IS NULL OR mv.[Status] NOT IN (N'Published', N'Active')) THEN 1
                    WHEN COALESCE(componentCounts.[ComponentCount], 0) = 0
                     AND COALESCE(recipeCounts.[LegacyRecipeCount], 0) = 0 THEN 1
                    ELSE 0
                END), 0) AS [QuickWarningCount]
            FROM [DietVariants] dv
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
                AND d.[IsDeleted] = 0
                AND d.[IsActive] = 1
            LEFT JOIN [DietMenuPlanItems] i ON i.[DietVariantId] = dv.[Id]
                AND i.[DietMenuPlanId] = @planId
                AND i.[IsDeleted] = 0
                AND i.[IsActive] = 1
            LEFT JOIN [Meals] m ON m.[Id] = i.[MealId]
                AND m.[IsDeleted] = 0
                AND m.[IsActive] = 1
            LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId]
                AND mv.[IsDeleted] = 0
            OUTER APPLY (
                SELECT COUNT(*) AS [ComponentCount]
                FROM (
                    SELECT mrc.[RecipeComponentVersionId]
                    FROM [MealRecipeComponents] mrc
                    WHERE i.[MealVariantId] IS NULL
                      AND mrc.[MealId] = i.[MealId]
                      AND mrc.[IsDeleted] = 0

                    UNION ALL

                    SELECT mvc.[RecipeComponentVersionId]
                    FROM [MealVariantComponents] mvc
                    WHERE mvc.[MealVariantId] = i.[MealVariantId]
                      AND mvc.[IsDeleted] = 0
                ) componentSource
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = componentSource.[RecipeComponentVersionId]
                WHERE rcv.[IsDeleted] = 0
            ) componentCounts
            OUTER APPLY (
                SELECT COUNT(*) AS [LegacyRecipeCount]
                FROM [Recipes] r
                WHERE r.[MealId] = i.[MealId]
                  AND r.[IsDeleted] = 0
            ) recipeCounts
            WHERE dv.[IsDeleted] = 0
            GROUP BY dv.[Id], d.[Name], dv.[Name], dv.[TargetCalories], dv.[IsDefault]
            ORDER BY d.[Name], dv.[TargetCalories], dv.[Name];
            """,
            new { planId });

        return rows.ToList();
    }

    public async Task<DietMenuPlanItemRow?> GetPlanItemAsync(int itemId)
    {
        using var db = this.factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<DietMenuPlanItemRow>(
            ItemSql("i.[Id] = @itemId"),
            new { itemId });
    }

    public async Task<IReadOnlyList<MealPlanSearchRow>> GetPublishedMealsForPlanningAsync()
    {
        using var db = this.factory.CreateConnection();
        var rows = await db.QueryAsync<MealPlanSearchRow>(
            """
            SELECT
                m.[Id] AS [MealId],
                m.[Name] AS [MealName],
                c.[Name] AS [CategoryName],
                m.[Status],
                COALESCE(componentCounts.[ComponentCount], 0) AS [ComponentCount],
                COALESCE(recipeCounts.[LegacyRecipeCount], 0) AS [LegacyRecipeCount]
            FROM [Meals] m
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            OUTER APPLY (
                SELECT COUNT(*) AS [ComponentCount]
                FROM [MealRecipeComponents] mrc
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                WHERE mrc.[MealId] = m.[Id]
                  AND mrc.[IsDeleted] = 0
                  AND rcv.[IsDeleted] = 0
            ) componentCounts
            OUTER APPLY (
                SELECT COUNT(*) AS [LegacyRecipeCount]
                FROM [Recipes] r
                WHERE r.[MealId] = m.[Id]
                  AND r.[IsDeleted] = 0
            ) recipeCounts
            WHERE m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND m.[Status] = N'Published'
            ORDER BY m.[Name];
            """);

        return rows.ToList();
    }

    public async Task<int> EnsurePlanAsync(DateOnly date, string? notes, string? userName)
    {
        using var db = this.factory.CreateConnection();
        var existing = await db.QuerySingleOrDefaultAsync<ExistingPlanRow>(
            """
            SELECT [Id], [IsDeleted]
            FROM [DietMenuPlans]
            WHERE [PlanDate] = @date;
            """,
            new { date });

        if (existing is { IsDeleted: false })
        {
            return existing.Id;
        }

        if (existing is not null)
        {
            await db.ExecuteAsync(
                """
                UPDATE [DietMenuPlans]
                SET [Status] = N'Draft',
                    [Notes] = @notes,
                    [PublishedAt] = NULL,
                    [PublishedBy] = NULL,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @userName,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [DeletedBy] = NULL
                WHERE [Id] = @id;
                """,
                new { id = existing.Id, notes, now = DateTimeOffset.UtcNow, userName });
            return existing.Id;
        }

        return await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [DietMenuPlans]
                ([PlanDate], [Status], [Notes], [CreatedAt], [CreatedBy], [IsDeleted])
            OUTPUT INSERTED.[Id]
            VALUES
                (@date, N'Draft', @notes, @now, @userName, 0);
            """,
            new { date, notes, now = DateTimeOffset.UtcNow, userName });
    }

    public async Task<int> AddItemAsync(DietMenuPlanItem item)
    {
        using var db = this.factory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [DietMenuPlanItems]
                ([DietMenuPlanId], [DietVariantId], [MealId], [MealVariantId], [MealSlot], [ServingSizeMultiplier], [SortOrder],
                 [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            OUTPUT INSERTED.[Id]
            VALUES
                (@DietMenuPlanId, @DietVariantId, @MealId, @MealVariantId, @MealSlot, @ServingSizeMultiplier, @SortOrder,
                 1, @CreatedAt, @CreatedBy, 0);
            """,
            item);
    }

    public async Task UpdateItemAsync(DietMenuPlanItem item)
    {
        using var db = this.factory.CreateConnection();
        await db.ExecuteAsync(
            """
            UPDATE [DietMenuPlanItems]
            SET [DietVariantId] = @DietVariantId,
                [MealId] = @MealId,
                [MealVariantId] = @MealVariantId,
                [MealSlot] = @MealSlot,
                [ServingSizeMultiplier] = @ServingSizeMultiplier,
                [SortOrder] = @SortOrder,
                [UpdatedAt] = @UpdatedAt,
                [UpdatedBy] = @UpdatedBy
            WHERE [Id] = @Id
              AND [IsDeleted] = 0;
            """,
            item);
    }

    public async Task SoftDeleteItemAsync(int itemId, string? userName)
    {
        using var db = this.factory.CreateConnection();
        await db.ExecuteAsync(
            """
            UPDATE [DietMenuPlanItems]
            SET [IsActive] = 0,
                [IsDeleted] = 1,
                [DeletedAt] = @now,
                [DeletedBy] = @userName,
                [UpdatedAt] = @now,
                [UpdatedBy] = @userName
            WHERE [Id] = @itemId;
            """,
            new { itemId, userName, now = DateTimeOffset.UtcNow });
    }

    public async Task<int> CopyDayAsync(int sourcePlanId, DateOnly targetDate, string? userName, bool clearTargetDraft)
    {
        using var db = this.factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

        var target = await db.QuerySingleOrDefaultAsync<ExistingPlanRow>(
            """
            SELECT [Id], [IsDeleted]
            FROM [DietMenuPlans]
            WHERE [PlanDate] = @targetDate;
            """,
            new { targetDate },
            tx);

        int targetPlanId;
        if (target is null)
        {
            targetPlanId = await db.ExecuteScalarAsync<int>(
                """
                INSERT INTO [DietMenuPlans]
                    ([PlanDate], [Status], [Notes], [CreatedAt], [CreatedBy], [IsDeleted])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@targetDate, N'Draft', N'Kopia dnia menu', @now, @userName, 0);
                """,
                new { targetDate, now, userName },
                tx);
        }
        else
        {
            targetPlanId = target.Id;
            await db.ExecuteAsync(
                """
                UPDATE [DietMenuPlans]
                SET [Status] = N'Draft',
                    [Notes] = COALESCE([Notes], N'Kopia dnia menu'),
                    [PublishedAt] = NULL,
                    [PublishedBy] = NULL,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @userName,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [DeletedBy] = NULL
                WHERE [Id] = @targetPlanId;
                """,
                new { targetPlanId, now, userName },
                tx);
        }

        if (clearTargetDraft)
        {
            await db.ExecuteAsync(
                """
                UPDATE [DietMenuPlanItems]
                SET [IsActive] = 0,
                    [IsDeleted] = 1,
                    [DeletedAt] = @now,
                    [DeletedBy] = @userName,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @userName
                WHERE [DietMenuPlanId] = @targetPlanId
                  AND [IsDeleted] = 0;
                """,
                new { targetPlanId, now, userName },
                tx);
        }

        await db.ExecuteAsync(
            """
            INSERT INTO [DietMenuPlanItems]
                ([DietMenuPlanId], [DietVariantId], [MealId], [MealVariantId], [MealSlot], [ServingSizeMultiplier], [SortOrder],
                 [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                @targetPlanId,
                [DietVariantId],
                [MealId],
                [MealVariantId],
                [MealSlot],
                [ServingSizeMultiplier],
                [SortOrder],
                1,
                @now,
                @userName,
                0
            FROM [DietMenuPlanItems]
            WHERE [DietMenuPlanId] = @sourcePlanId
              AND [IsDeleted] = 0
              AND [IsActive] = 1;
            """,
            new { sourcePlanId, targetPlanId, now, userName },
            tx);

        tx.Commit();
        return targetPlanId;
    }

    public async Task PublishAsync(int planId, string? userName)
    {
        using var db = this.factory.CreateConnection();
        await db.ExecuteAsync(
            """
            UPDATE [DietMenuPlans]
            SET [Status] = N'Published',
                [PublishedAt] = @now,
                [PublishedBy] = @userName,
                [UpdatedAt] = @now,
                [UpdatedBy] = @userName
            WHERE [Id] = @planId
              AND [IsDeleted] = 0;
            """,
            new { planId, userName, now = DateTimeOffset.UtcNow });
    }

    private static string DaySql(string predicate)
    {
        return $$"""
            SELECT
                p.[Id],
                p.[PlanDate],
                p.[Status],
                p.[Notes],
                p.[PublishedAt],
                p.[PublishedBy],
                COUNT(i.[Id]) AS [ActiveItemCount]
            FROM [DietMenuPlans] p
            LEFT JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
                AND i.[IsDeleted] = 0
                AND i.[IsActive] = 1
            WHERE p.[IsDeleted] = 0
              AND {{predicate}}
            GROUP BY p.[Id], p.[PlanDate], p.[Status], p.[Notes], p.[PublishedAt], p.[PublishedBy];
            """;
    }

    private static string ItemSql(string predicate)
    {
        return $$"""
            SELECT
                i.[Id],
                i.[DietMenuPlanId],
                p.[PlanDate],
                p.[Status] AS [PlanStatus],
                i.[DietVariantId],
                d.[Name] AS [DietName],
                dv.[Name] AS [VariantName],
                i.[MealId],
                i.[MealVariantId],
                mv.[Name] AS [MealVariantName],
                mv.[Status] AS [MealVariantStatus],
                m.[Name] AS [MealName],
                m.[Status] AS [MealStatus],
                i.[MealSlot],
                i.[ServingSizeMultiplier],
                i.[SortOrder],
                COALESCE(componentCounts.[ComponentCount], 0) AS [ComponentCount],
                COALESCE(recipeCounts.[LegacyRecipeCount], 0) AS [LegacyRecipeCount],
                CASE
                    WHEN nf.[MealId] IS NOT NULL
                     AND nf.[CaloriesPer100g] IS NOT NULL
                     AND nf.[ProteinPer100g] IS NOT NULL
                     AND nf.[CarbohydratesPer100g] IS NOT NULL
                     AND nf.[FatPer100g] IS NOT NULL
                     AND nf.[FiberPer100g] IS NOT NULL THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS [HasNutrition],
                COALESCE(allergenCounts.[AllergenCount], 0) AS [AllergenCount],
                COALESCE(packagingCounts.[PackagingRequirementCount], 0) AS [PackagingRequirementCount],
                COALESCE(mappingCounts.[MissingWarehouseCategoryCount], 0) AS [MissingWarehouseCategoryCount]
            FROM [DietMenuPlanItems] i
            INNER JOIN [DietMenuPlans] p ON p.[Id] = i.[DietMenuPlanId]
            INNER JOIN [DietVariants] dv ON dv.[Id] = i.[DietVariantId]
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
            INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
            LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId] AND mv.[IsDeleted] = 0
            LEFT JOIN [NutritionFacts] nf ON nf.[MealId] = m.[Id]
            OUTER APPLY (
                SELECT COUNT(*) AS [ComponentCount]
                FROM (
                    SELECT mrc.[RecipeComponentVersionId]
                    FROM [MealRecipeComponents] mrc
                    WHERE i.[MealVariantId] IS NULL
                      AND mrc.[MealId] = i.[MealId]
                      AND mrc.[IsDeleted] = 0

                    UNION ALL

                    SELECT mvc.[RecipeComponentVersionId]
                    FROM [MealVariantComponents] mvc
                    WHERE mvc.[MealVariantId] = i.[MealVariantId]
                      AND mvc.[IsDeleted] = 0
                ) componentSource
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = componentSource.[RecipeComponentVersionId]
                WHERE rcv.[IsDeleted] = 0
            ) componentCounts
            OUTER APPLY (
                SELECT COUNT(*) AS [LegacyRecipeCount]
                FROM [Recipes] r
                WHERE r.[MealId] = i.[MealId]
                  AND r.[IsDeleted] = 0
            ) recipeCounts
            OUTER APPLY (
                SELECT COUNT(DISTINCT [AllergenId]) AS [AllergenCount]
                FROM (
                    SELECT ma.[AllergenId]
                    FROM [MealAllergens] ma
                    WHERE ma.[MealId] = i.[MealId]

                    UNION

                    SELECT ia.[AllergenId]
                    FROM [Recipes] r
                    INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = r.[IngredientId]
                    WHERE i.[MealVariantId] IS NULL
                      AND r.[MealId] = i.[MealId]
                      AND r.[IsDeleted] = 0

                    UNION

                    SELECT ia.[AllergenId]
                    FROM [MealRecipeComponents] mrc
                    INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = mrc.[RecipeComponentVersionId]
                    INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = rci.[IngredientId]
                    WHERE i.[MealVariantId] IS NULL
                      AND mrc.[MealId] = i.[MealId]
                      AND mrc.[IsDeleted] = 0
                      AND rci.[IsDeleted] = 0

                    UNION

                    SELECT ia.[AllergenId]
                    FROM [MealVariantComponents] mvc
                    INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = mvc.[RecipeComponentVersionId]
                    INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = rci.[IngredientId]
                    WHERE mvc.[MealVariantId] = i.[MealVariantId]
                      AND mvc.[IsDeleted] = 0
                      AND rci.[IsDeleted] = 0
                ) allergens
            ) allergenCounts
            OUTER APPLY (
                SELECT COUNT(*) AS [PackagingRequirementCount]
                FROM [PackagingRequirements] pr
                WHERE pr.[IsDeleted] = 0
                  AND (
                        pr.[MealId] = i.[MealId]
                        OR pr.[MealVariantId] = i.[MealVariantId]
                        OR pr.[RecipeComponentVersionId] IN (
                            SELECT mrc.[RecipeComponentVersionId]
                            FROM [MealRecipeComponents] mrc
                            WHERE mrc.[MealId] = i.[MealId]
                              AND mrc.[IsDeleted] = 0
                            UNION
                            SELECT mvc.[RecipeComponentVersionId]
                            FROM [MealVariantComponents] mvc
                            WHERE mvc.[MealVariantId] = i.[MealVariantId]
                              AND mvc.[IsDeleted] = 0
                        )
                  )
            ) packagingCounts
            OUTER APPLY (
                SELECT COUNT(*) AS [MissingWarehouseCategoryCount]
                FROM (
                    SELECT COALESCE(rci.[WarehouseCategoryId], ing.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId]
                    FROM [MealRecipeComponents] mrc
                    INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = mrc.[RecipeComponentVersionId]
                    INNER JOIN [Ingredients] ing ON ing.[Id] = rci.[IngredientId]
                    LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = ing.[Id] AND si.[IsDeleted] = 0
                    WHERE i.[MealVariantId] IS NULL
                      AND mrc.[MealId] = i.[MealId]
                      AND mrc.[IsDeleted] = 0
                      AND rci.[IsDeleted] = 0
                      AND ing.[IsDeleted] = 0

                    UNION ALL

                    SELECT COALESCE(rci.[WarehouseCategoryId], ing.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId]
                    FROM [MealVariantComponents] mvc
                    INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = mvc.[RecipeComponentVersionId]
                    INNER JOIN [Ingredients] ing ON ing.[Id] = rci.[IngredientId]
                    LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = ing.[Id] AND si.[IsDeleted] = 0
                    WHERE mvc.[MealVariantId] = i.[MealVariantId]
                      AND mvc.[IsDeleted] = 0
                      AND rci.[IsDeleted] = 0
                      AND ing.[IsDeleted] = 0

                    UNION ALL

                    SELECT COALESCE(ing.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId]
                    FROM [Recipes] r
                    INNER JOIN [Ingredients] ing ON ing.[Id] = r.[IngredientId]
                    LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = ing.[Id] AND si.[IsDeleted] = 0
                    WHERE i.[MealVariantId] IS NULL
                      AND r.[MealId] = i.[MealId]
                      AND r.[IsDeleted] = 0
                      AND ing.[IsDeleted] = 0
                ) mappings
                WHERE mappings.[WarehouseCategoryId] IS NULL
            ) mappingCounts
            WHERE i.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND p.[IsDeleted] = 0
              AND {{predicate}}
            ORDER BY i.[SortOrder], i.[Id];
            """;
    }

    private sealed class ExistingPlanRow
    {
        public int Id { get; set; }

        public bool IsDeleted { get; set; }
    }
}
