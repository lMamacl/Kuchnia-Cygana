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
            WITH FilteredPlans AS (
                SELECT
                    p.[Id],
                    p.[PlanDate],
                    p.[Status],
                    p.[PublishedAt],
                    p.[PublishedBy]
                FROM [DietMenuPlans] p
                WHERE p.[IsDeleted] = 0
                  AND p.[PlanDate] >= @startDate
                  AND p.[PlanDate] <= @endDate
            ),
            FilteredItems AS (
                SELECT
                    i.[Id],
                    i.[DietMenuPlanId],
                    i.[MealId],
                    i.[MealVariantId],
                    m.[Id] AS [ResolvedMealId],
                    m.[Status] AS [MealStatus],
                    mv.[Id] AS [ResolvedMealVariantId],
                    mv.[Status] AS [MealVariantStatus]
                FROM FilteredPlans p
                LEFT JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
                    AND i.[IsDeleted] = 0
                    AND i.[IsActive] = 1
                LEFT JOIN [Meals] m ON m.[Id] = i.[MealId]
                    AND m.[IsDeleted] = 0
                    AND m.[IsActive] = 1
                LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId]
                    AND mv.[IsDeleted] = 0
            ),
            ComponentSources AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], mrc.[RecipeComponentVersionId]
                FROM FilteredItems fi
                INNER JOIN [MealRecipeComponents] mrc ON fi.[MealVariantId] IS NULL
                    AND mrc.[MealId] = fi.[MealId]
                    AND mrc.[IsDeleted] = 0
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0

                UNION ALL

                SELECT fi.[Id] AS [DietMenuPlanItemId], mvc.[RecipeComponentVersionId]
                FROM FilteredItems fi
                INNER JOIN [MealVariantComponents] mvc ON mvc.[MealVariantId] = fi.[MealVariantId]
                    AND mvc.[IsDeleted] = 0
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mvc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0
            ),
            ComponentCounts AS (
                SELECT [DietMenuPlanItemId], COUNT(*) AS [ComponentCount]
                FROM ComponentSources
                GROUP BY [DietMenuPlanItemId]
            ),
            LegacyRecipeCounts AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], COUNT(*) AS [LegacyRecipeCount]
                FROM FilteredItems fi
                INNER JOIN [Recipes] r ON r.[MealId] = fi.[MealId]
                    AND r.[IsDeleted] = 0
                GROUP BY fi.[Id]
            ),
            ItemWarnings AS (
                SELECT
                    fi.[DietMenuPlanId],
                    fi.[Id],
                    CASE
                        WHEN fi.[Id] IS NULL THEN 0
                        WHEN fi.[ResolvedMealId] IS NULL THEN 1
                        WHEN fi.[MealStatus] NOT IN (N'Published', N'Active') THEN 1
                        WHEN fi.[MealVariantId] IS NOT NULL
                         AND (fi.[ResolvedMealVariantId] IS NULL OR fi.[MealVariantStatus] NOT IN (N'Published', N'Active')) THEN 1
                        WHEN COALESCE(componentCounts.[ComponentCount], 0) = 0
                         AND COALESCE(recipeCounts.[LegacyRecipeCount], 0) = 0 THEN 1
                        ELSE 0
                    END AS [QuickWarning]
                FROM FilteredItems fi
                LEFT JOIN ComponentCounts componentCounts ON componentCounts.[DietMenuPlanItemId] = fi.[Id]
                LEFT JOIN LegacyRecipeCounts recipeCounts ON recipeCounts.[DietMenuPlanItemId] = fi.[Id]
            )
            SELECT
                p.[Id],
                p.[PlanDate],
                p.[Status],
                p.[PublishedAt],
                p.[PublishedBy],
                COUNT(i.[Id]) AS [ActiveItemCount],
                COALESCE(SUM(w.[QuickWarning]), 0) AS [QuickWarningCount]
            FROM FilteredPlans p
            LEFT JOIN FilteredItems i ON i.[DietMenuPlanId] = p.[Id]
            LEFT JOIN ItemWarnings w ON w.[Id] = i.[Id]
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
            WITH FilteredVariants AS (
                SELECT
                    dv.[Id] AS [DietVariantId],
                    d.[Name] AS [DietName],
                    dv.[Name] AS [VariantName],
                    dv.[TargetCalories],
                    dv.[IsDefault]
                FROM [DietVariants] dv
                INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
                    AND d.[IsDeleted] = 0
                    AND d.[IsActive] = 1
                WHERE dv.[IsDeleted] = 0
            ),
            FilteredItems AS (
                SELECT
                    fv.[DietVariantId],
                    i.[Id],
                    i.[MealId],
                    i.[MealVariantId],
                    m.[Id] AS [ResolvedMealId],
                    m.[Status] AS [MealStatus],
                    mv.[Id] AS [ResolvedMealVariantId],
                    mv.[Status] AS [MealVariantStatus]
                FROM FilteredVariants fv
                LEFT JOIN [DietMenuPlanItems] i ON i.[DietVariantId] = fv.[DietVariantId]
                    AND i.[DietMenuPlanId] = @planId
                    AND i.[IsDeleted] = 0
                    AND i.[IsActive] = 1
                LEFT JOIN [Meals] m ON m.[Id] = i.[MealId]
                    AND m.[IsDeleted] = 0
                    AND m.[IsActive] = 1
                LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId]
                    AND mv.[IsDeleted] = 0
            ),
            ComponentSources AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], mrc.[RecipeComponentVersionId]
                FROM FilteredItems fi
                INNER JOIN [MealRecipeComponents] mrc ON fi.[MealVariantId] IS NULL
                    AND mrc.[MealId] = fi.[MealId]
                    AND mrc.[IsDeleted] = 0
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0

                UNION ALL

                SELECT fi.[Id] AS [DietMenuPlanItemId], mvc.[RecipeComponentVersionId]
                FROM FilteredItems fi
                INNER JOIN [MealVariantComponents] mvc ON mvc.[MealVariantId] = fi.[MealVariantId]
                    AND mvc.[IsDeleted] = 0
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mvc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0
            ),
            ComponentCounts AS (
                SELECT [DietMenuPlanItemId], COUNT(*) AS [ComponentCount]
                FROM ComponentSources
                GROUP BY [DietMenuPlanItemId]
            ),
            LegacyRecipeCounts AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], COUNT(*) AS [LegacyRecipeCount]
                FROM FilteredItems fi
                INNER JOIN [Recipes] r ON r.[MealId] = fi.[MealId]
                    AND r.[IsDeleted] = 0
                GROUP BY fi.[Id]
            ),
            ItemWarnings AS (
                SELECT
                    fi.[DietVariantId],
                    fi.[Id],
                    CASE
                        WHEN fi.[Id] IS NULL THEN 0
                        WHEN fi.[ResolvedMealId] IS NULL THEN 1
                        WHEN fi.[MealStatus] NOT IN (N'Published', N'Active') THEN 1
                        WHEN fi.[MealVariantId] IS NOT NULL
                         AND (fi.[ResolvedMealVariantId] IS NULL OR fi.[MealVariantStatus] NOT IN (N'Published', N'Active')) THEN 1
                        WHEN COALESCE(componentCounts.[ComponentCount], 0) = 0
                         AND COALESCE(recipeCounts.[LegacyRecipeCount], 0) = 0 THEN 1
                        ELSE 0
                    END AS [QuickWarning]
                FROM FilteredItems fi
                LEFT JOIN ComponentCounts componentCounts ON componentCounts.[DietMenuPlanItemId] = fi.[Id]
                LEFT JOIN LegacyRecipeCounts recipeCounts ON recipeCounts.[DietMenuPlanItemId] = fi.[Id]
            )
            SELECT
                fv.[DietVariantId],
                fv.[DietName],
                fv.[VariantName],
                fv.[TargetCalories],
                fv.[IsDefault],
                COUNT(i.[Id]) AS [ActiveItemCount],
                COALESCE(SUM(w.[QuickWarning]), 0) AS [QuickWarningCount]
            FROM FilteredVariants fv
            LEFT JOIN FilteredItems i ON i.[DietVariantId] = fv.[DietVariantId]
            LEFT JOIN ItemWarnings w ON w.[Id] = i.[Id]
            GROUP BY fv.[DietVariantId], fv.[DietName], fv.[VariantName], fv.[TargetCalories], fv.[IsDefault]
            ORDER BY fv.[DietName], fv.[TargetCalories], fv.[VariantName];
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
            WITH PublishedMeals AS (
                SELECT
                    m.[Id],
                    m.[Name],
                    m.[CategoryId],
                    m.[Status]
                FROM [Meals] m
                WHERE m.[IsDeleted] = 0
                  AND m.[IsActive] = 1
                  AND m.[Status] = N'Published'
            ),
            ComponentCounts AS (
                SELECT mrc.[MealId], COUNT(*) AS [ComponentCount]
                FROM [MealRecipeComponents] mrc
                INNER JOIN PublishedMeals m ON m.[Id] = mrc.[MealId]
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0
                WHERE mrc.[IsDeleted] = 0
                GROUP BY mrc.[MealId]
            ),
            LegacyRecipeCounts AS (
                SELECT r.[MealId], COUNT(*) AS [LegacyRecipeCount]
                FROM [Recipes] r
                INNER JOIN PublishedMeals m ON m.[Id] = r.[MealId]
                WHERE r.[IsDeleted] = 0
                GROUP BY r.[MealId]
            )
            SELECT
                m.[Id] AS [MealId],
                m.[Name] AS [MealName],
                c.[Name] AS [CategoryName],
                m.[Status],
                COALESCE(componentCounts.[ComponentCount], 0) AS [ComponentCount],
                COALESCE(recipeCounts.[LegacyRecipeCount], 0) AS [LegacyRecipeCount]
            FROM PublishedMeals m
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            LEFT JOIN ComponentCounts componentCounts ON componentCounts.[MealId] = m.[Id]
            LEFT JOIN LegacyRecipeCounts recipeCounts ON recipeCounts.[MealId] = m.[Id]
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

    public async Task PublishAsync(
        int planId,
        string? userName,
        IReadOnlyList<DietMenuPlanPublishedSnapshotRow> snapshots,
        string? planSnapshotHash)
    {
        using var db = this.factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

        await db.ExecuteAsync(
            """
            UPDATE [DietMenuPlans]
            SET [Status] = N'Published',
                [PublishedAt] = @now,
                [PublishedBy] = @userName,
                [PublishedSnapshotHash] = @planSnapshotHash,
                [PublishedSnapshotItemCount] = @snapshotCount,
                [UpdatedAt] = @now,
                [UpdatedBy] = @userName
            WHERE [Id] = @planId
              AND [IsDeleted] = 0;
            """,
            new
            {
                planId,
                userName,
                planSnapshotHash,
                snapshotCount = snapshots.Count,
                now,
            },
            tx);

        if (snapshots.Count > 0)
        {
            await db.ExecuteAsync(
                """
                UPDATE [DietMenuPlanItems]
                SET [PublishedSnapshotJson] = @SnapshotJson,
                    [PublishedSnapshotHash] = @SnapshotHash,
                    [PublishedSnapshotCreatedAt] = @now,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @userName
                WHERE [Id] = @DietMenuPlanItemId
                  AND [DietMenuPlanId] = @planId
                  AND [IsDeleted] = 0;
                """,
                snapshots.Select(snapshot => new
                {
                    snapshot.DietMenuPlanItemId,
                    snapshot.SnapshotJson,
                    snapshot.SnapshotHash,
                    planId,
                    now,
                    userName,
                }),
                tx);
        }

        tx.Commit();
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
            WITH FilteredItems AS (
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
                    CASE
                        WHEN nf.[MealId] IS NOT NULL
                         AND nf.[CaloriesPer100g] IS NOT NULL
                         AND nf.[ProteinPer100g] IS NOT NULL
                         AND nf.[CarbohydratesPer100g] IS NOT NULL
                         AND nf.[FatPer100g] IS NOT NULL
                         AND nf.[FiberPer100g] IS NOT NULL THEN CAST(1 AS bit)
                        ELSE CAST(0 AS bit)
                    END AS [HasNutrition]
                FROM [DietMenuPlanItems] i
                INNER JOIN [DietMenuPlans] p ON p.[Id] = i.[DietMenuPlanId]
                INNER JOIN [DietVariants] dv ON dv.[Id] = i.[DietVariantId]
                INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
                INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
                LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId] AND mv.[IsDeleted] = 0
                LEFT JOIN [NutritionFacts] nf ON nf.[MealId] = m.[Id]
                WHERE i.[IsDeleted] = 0
                  AND i.[IsActive] = 1
                  AND p.[IsDeleted] = 0
                  AND {{predicate}}
            ),
            ComponentSources AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], mrc.[RecipeComponentVersionId]
                FROM FilteredItems fi
                INNER JOIN [MealRecipeComponents] mrc ON fi.[MealVariantId] IS NULL
                    AND mrc.[MealId] = fi.[MealId]
                    AND mrc.[IsDeleted] = 0
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0

                UNION ALL

                SELECT fi.[Id] AS [DietMenuPlanItemId], mvc.[RecipeComponentVersionId]
                FROM FilteredItems fi
                INNER JOIN [MealVariantComponents] mvc ON mvc.[MealVariantId] = fi.[MealVariantId]
                    AND mvc.[IsDeleted] = 0
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mvc.[RecipeComponentVersionId]
                    AND rcv.[IsDeleted] = 0
            ),
            ComponentCounts AS (
                SELECT [DietMenuPlanItemId], COUNT(*) AS [ComponentCount]
                FROM ComponentSources
                GROUP BY [DietMenuPlanItemId]
            ),
            LegacyRecipeCounts AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], COUNT(*) AS [LegacyRecipeCount]
                FROM FilteredItems fi
                INNER JOIN [Recipes] r ON r.[MealId] = fi.[MealId]
                    AND r.[IsDeleted] = 0
                GROUP BY fi.[Id]
            ),
            AllergenSources AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], ma.[AllergenId]
                FROM FilteredItems fi
                INNER JOIN [MealAllergens] ma ON ma.[MealId] = fi.[MealId]

                UNION

                SELECT fi.[Id] AS [DietMenuPlanItemId], ia.[AllergenId]
                FROM FilteredItems fi
                INNER JOIN [Recipes] r ON fi.[MealVariantId] IS NULL
                    AND r.[MealId] = fi.[MealId]
                    AND r.[IsDeleted] = 0
                INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = r.[IngredientId]

                UNION

                SELECT cs.[DietMenuPlanItemId], ia.[AllergenId]
                FROM ComponentSources cs
                INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = cs.[RecipeComponentVersionId]
                    AND rci.[IsDeleted] = 0
                INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = rci.[IngredientId]
            ),
            AllergenCounts AS (
                SELECT [DietMenuPlanItemId], COUNT(DISTINCT [AllergenId]) AS [AllergenCount]
                FROM AllergenSources
                GROUP BY [DietMenuPlanItemId]
            ),
            PackagingSources AS (
                SELECT fi.[Id] AS [DietMenuPlanItemId], pr.[Id] AS [PackagingRequirementId]
                FROM FilteredItems fi
                INNER JOIN [PackagingRequirements] pr ON pr.[MealId] = fi.[MealId]
                    AND pr.[IsDeleted] = 0

                UNION

                SELECT fi.[Id] AS [DietMenuPlanItemId], pr.[Id] AS [PackagingRequirementId]
                FROM FilteredItems fi
                INNER JOIN [PackagingRequirements] pr ON pr.[MealVariantId] = fi.[MealVariantId]
                    AND pr.[IsDeleted] = 0

                UNION

                SELECT cs.[DietMenuPlanItemId], pr.[Id] AS [PackagingRequirementId]
                FROM ComponentSources cs
                INNER JOIN [PackagingRequirements] pr ON pr.[RecipeComponentVersionId] = cs.[RecipeComponentVersionId]
                    AND pr.[IsDeleted] = 0
            ),
            PackagingCounts AS (
                SELECT [DietMenuPlanItemId], COUNT(DISTINCT [PackagingRequirementId]) AS [PackagingRequirementCount]
                FROM PackagingSources
                GROUP BY [DietMenuPlanItemId]
            ),
            MappingSources AS (
                SELECT
                    cs.[DietMenuPlanItemId],
                    COALESCE(rci.[WarehouseCategoryId], ing.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId]
                FROM ComponentSources cs
                INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = cs.[RecipeComponentVersionId]
                    AND rci.[IsDeleted] = 0
                INNER JOIN [Ingredients] ing ON ing.[Id] = rci.[IngredientId]
                    AND ing.[IsDeleted] = 0
                LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = ing.[Id] AND si.[IsDeleted] = 0

                UNION ALL

                SELECT
                    fi.[Id] AS [DietMenuPlanItemId],
                    COALESCE(ing.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId]
                FROM FilteredItems fi
                INNER JOIN [Recipes] r ON fi.[MealVariantId] IS NULL
                    AND r.[MealId] = fi.[MealId]
                    AND r.[IsDeleted] = 0
                INNER JOIN [Ingredients] ing ON ing.[Id] = r.[IngredientId]
                    AND ing.[IsDeleted] = 0
                LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = ing.[Id] AND si.[IsDeleted] = 0
            ),
            MappingCounts AS (
                SELECT [DietMenuPlanItemId], COUNT(*) AS [MissingWarehouseCategoryCount]
                FROM MappingSources
                WHERE [WarehouseCategoryId] IS NULL
                GROUP BY [DietMenuPlanItemId]
            )
            SELECT
                fi.[Id],
                fi.[DietMenuPlanId],
                fi.[PlanDate],
                fi.[PlanStatus],
                fi.[DietVariantId],
                fi.[DietName],
                fi.[VariantName],
                fi.[MealId],
                fi.[MealVariantId],
                fi.[MealVariantName],
                fi.[MealVariantStatus],
                fi.[MealName],
                fi.[MealStatus],
                fi.[MealSlot],
                fi.[ServingSizeMultiplier],
                fi.[SortOrder],
                COALESCE(componentCounts.[ComponentCount], 0) AS [ComponentCount],
                COALESCE(recipeCounts.[LegacyRecipeCount], 0) AS [LegacyRecipeCount],
                fi.[HasNutrition],
                COALESCE(allergenCounts.[AllergenCount], 0) AS [AllergenCount],
                COALESCE(packagingCounts.[PackagingRequirementCount], 0) AS [PackagingRequirementCount],
                COALESCE(mappingCounts.[MissingWarehouseCategoryCount], 0) AS [MissingWarehouseCategoryCount]
            FROM FilteredItems fi
            LEFT JOIN ComponentCounts componentCounts ON componentCounts.[DietMenuPlanItemId] = fi.[Id]
            LEFT JOIN LegacyRecipeCounts recipeCounts ON recipeCounts.[DietMenuPlanItemId] = fi.[Id]
            LEFT JOIN AllergenCounts allergenCounts ON allergenCounts.[DietMenuPlanItemId] = fi.[Id]
            LEFT JOIN PackagingCounts packagingCounts ON packagingCounts.[DietMenuPlanItemId] = fi.[Id]
            LEFT JOIN MappingCounts mappingCounts ON mappingCounts.[DietMenuPlanItemId] = fi.[Id]
            ORDER BY fi.[SortOrder], fi.[Id];
            """;
    }

    private sealed class ExistingPlanRow
    {
        public int Id { get; set; }

        public bool IsDeleted { get; set; }
    }
}
