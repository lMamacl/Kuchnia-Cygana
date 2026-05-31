using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class RecipeComponentRepository : IRecipeComponentRepository
{
    private readonly IDbConnectionFactory factory;

    public RecipeComponentRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IReadOnlyList<RecipeComponentListRow>> SearchComponentsAsync(string? query)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<RecipeComponentListRow>(
            """
            WITH LatestVersions AS (
                SELECT
                    rcv.[RecipeComponentId],
                    rcv.[Id],
                    rcv.[VersionNumber],
                    rcv.[Status],
                    ROW_NUMBER() OVER (
                        PARTITION BY rcv.[RecipeComponentId]
                        ORDER BY rcv.[VersionNumber] DESC, rcv.[Id] DESC
                    ) AS [RowNumber]
                FROM [RecipeComponentVersions] rcv
                WHERE rcv.[IsDeleted] = 0
            )
            SELECT
                rc.[Id],
                rc.[Name],
                rc.[Description],
                rc.[IsActive],
                COUNT(rcv.[Id]) AS [VersionCount],
                lv.[Id] AS [LatestVersionId],
                lv.[VersionNumber] AS [LatestVersionNumber],
                lv.[Status] AS [LatestVersionStatus]
            FROM [RecipeComponents] rc
            LEFT JOIN [RecipeComponentVersions] rcv ON rcv.[RecipeComponentId] = rc.[Id] AND rcv.[IsDeleted] = 0
            LEFT JOIN LatestVersions lv ON lv.[RecipeComponentId] = rc.[Id] AND lv.[RowNumber] = 1
            WHERE rc.[IsDeleted] = 0
              AND (@query IS NULL OR rc.[Name] LIKE @like OR rc.[Description] LIKE @like)
            GROUP BY rc.[Id], rc.[Name], rc.[Description], rc.[IsActive], lv.[Id], lv.[VersionNumber], lv.[Status]
            ORDER BY rc.[Name];
            """,
            new
            {
                query = string.IsNullOrWhiteSpace(query) ? null : query,
                like = $"%{query}%",
            });

        return rows.ToList();
    }

    public async Task<RecipeComponentDetailRow?> GetComponentAsync(int componentId)
    {
        using var db = this.factory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<RecipeComponentDetailRow>(
            """
            SELECT [Id], [Name], [Description], [IsActive]
            FROM [RecipeComponents]
            WHERE [Id] = @componentId
              AND [IsDeleted] = 0;
            """,
            new { componentId });
    }

    public async Task<IReadOnlyList<RecipeComponentVersionRow>> GetComponentVersionsAsync(int componentId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<RecipeComponentVersionRow>(
            VersionSelectSql() +
            """
            WHERE rcv.[RecipeComponentId] = @componentId
              AND rcv.[IsDeleted] = 0
            ORDER BY rcv.[VersionNumber] DESC, rcv.[Id] DESC;
            """,
            new { componentId });

        return rows.ToList();
    }

    public async Task<RecipeComponentVersionRow?> GetVersionAsync(int versionId)
    {
        using var db = this.factory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<RecipeComponentVersionRow>(
            VersionSelectSql() +
            """
            WHERE rcv.[Id] = @versionId
              AND rcv.[IsDeleted] = 0;
            """,
            new { versionId });
    }

    public async Task<IReadOnlyList<RecipeComponentIngredientRow>> GetVersionIngredientsAsync(int versionId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<RecipeComponentIngredientRow>(
            """
            SELECT
                rci.[Id],
                rci.[RecipeComponentVersionId],
                rci.[IngredientId],
                i.[Name] AS [IngredientName],
                COALESCE(rci.[StockItemId], i.[StockItemId]) AS [StockItemId],
                COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                wc.[Name] AS [WarehouseCategoryName],
                rci.[WeightInGrams],
                rci.[YieldFactor],
                rci.[IsOptional],
                rci.[Notes]
            FROM [RecipeComponentIngredients] rci
            INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId])
            WHERE rci.[RecipeComponentVersionId] = @versionId
              AND rci.[IsDeleted] = 0
            ORDER BY rci.[Id];
            """,
            new { versionId });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<PackagingRequirementRow>> GetVersionPackagingAsync(int versionId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<PackagingRequirementRow>(
            """
            SELECT
                [Id],
                [OwnerType],
                [MealId],
                [RecipeComponentVersionId],
                [StockItemId],
                [WarehouseCategoryId],
                [ResourceName],
                [Quantity],
                [Unit],
                [ContainerRole],
                [IsCustomerFacing]
            FROM [PackagingRequirements]
            WHERE [RecipeComponentVersionId] = @versionId
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { versionId });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<RecipeComponentVersionOptionRow>> GetPublishedVersionOptionsAsync()
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<RecipeComponentVersionOptionRow>(
            """
            SELECT
                rcv.[Id] AS [RecipeComponentVersionId],
                rc.[Id] AS [RecipeComponentId],
                rc.[Name] AS [ComponentName],
                rcv.[VersionNumber]
            FROM [RecipeComponentVersions] rcv
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            WHERE rcv.[Status] = N'Published'
              AND rcv.[IsDeleted] = 0
              AND rc.[IsDeleted] = 0
              AND rc.[IsActive] = 1
            ORDER BY rc.[Name], rcv.[VersionNumber] DESC;
            """);

        return rows.ToList();
    }

    public async Task<IEnumerable<MealRecipeComponentDetailsRow>> GetMealComponentDetailsAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();

        var componentRows = (await db.QueryAsync<MealRecipeComponentDetailsRow>(
            """
            SELECT
                rc.[Id] AS [RecipeComponentId],
                rcv.[Id] AS [RecipeComponentVersionId],
                rc.[Name] AS [ComponentName],
                rcv.[VersionNumber],
                rcv.[Status] AS [VersionStatus],
                mrc.[Role],
                mrc.[QuantityPerServing],
                mrc.[Unit],
                mrc.[SortOrder],
                rcv.[Instructions],
                rcv.[ShelfLifeHours],
                rcv.[UseEarliestIngredientExpiry],
                rci.[IngredientId],
                i.[Name] AS [IngredientName],
                COALESCE(rci.[StockItemId], i.[StockItemId], si.[Id]) AS [StockItemId],
                COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                wc.[Name] AS [WarehouseCategoryName],
                rci.[WeightInGrams],
                CASE
                    WHEN rci.[YieldFactor] <= 0 THEN i.[YieldFactor]
                    ELSE rci.[YieldFactor]
                END AS [YieldFactor],
                rci.[IsOptional],
                rci.[Notes]
            FROM [MealRecipeComponents] mrc
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            LEFT JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = rcv.[Id] AND rci.[IsDeleted] = 0
            LEFT JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId], si.[WarehouseCategoryId])
            WHERE mrc.[MealId] = @mealId
              AND mrc.[IsDeleted] = 0
              AND rcv.[IsDeleted] = 0
              AND rc.[IsDeleted] = 0
            ORDER BY mrc.[SortOrder], mrc.[Id], rci.[Id];
            """,
            new { mealId })).ToList();

        if (componentRows.Count > 0)
        {
            return componentRows;
        }

        return await db.QueryAsync<MealRecipeComponentDetailsRow>(
            """
            SELECT
                0 AS [RecipeComponentId],
                -r.[MealId] AS [RecipeComponentVersionId],
                CONCAT(m.[Name], N' (legacy Recipes)') AS [ComponentName],
                1 AS [VersionNumber],
                N'Legacy' AS [VersionStatus],
                N'legacy' AS [Role],
                CAST(1.0 AS decimal(10, 3)) AS [QuantityPerServing],
                N'portion' AS [Unit],
                0 AS [SortOrder],
                m.[PreparationInstructions] AS [Instructions],
                m.[ShelfLifeHours],
                m.[UseEarliestIngredientExpiry],
                r.[IngredientId],
                i.[Name] AS [IngredientName],
                COALESCE(i.[StockItemId], si.[Id]) AS [StockItemId],
                COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                wc.[Name] AS [WarehouseCategoryName],
                r.[WeightInGrams],
                i.[YieldFactor],
                r.[IsOptional],
                r.[Notes]
            FROM [Recipes] r
            INNER JOIN [Meals] m ON m.[Id] = r.[MealId]
            INNER JOIN [Ingredients] i ON i.[Id] = r.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId])
            WHERE r.[MealId] = @mealId
              AND i.[IsActive] = 1
              AND i.[IsDeleted] = 0
            ORDER BY r.[Id];
            """,
            new { mealId });
    }

    public async Task<bool> HasProductionPackagingAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();

        var count = await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM [PackagingRequirements] pr
            WHERE pr.[IsDeleted] = 0
              AND (
                    pr.[MealId] = @mealId
                    OR pr.[RecipeComponentVersionId] IN (
                        SELECT mrc.[RecipeComponentVersionId]
                        FROM [MealRecipeComponents] mrc
                        WHERE mrc.[MealId] = @mealId
                          AND mrc.[IsDeleted] = 0
                    )
              );
            """,
            new { mealId });

        return count > 0;
    }

    public async Task<int> CreateComponentAsync(RecipeComponent component)
    {
        using var db = this.factory.CreateConnection();

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [RecipeComponents]
                ([Name], [Description], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@Name, @Description, @IsActive, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            component);
    }

    public async Task<int> CreateVersionAsync(RecipeComponentVersion version, int? sourceVersionId)
    {
        using var db = this.factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        var nextVersionNumber = await db.ExecuteScalarAsync<int>(
            """
            SELECT ISNULL(MAX([VersionNumber]), 0) + 1
            FROM [RecipeComponentVersions]
            WHERE [RecipeComponentId] = @componentId
              AND [IsDeleted] = 0;
            """,
            new { componentId = version.RecipeComponentId },
            tx);

        version.VersionNumber = nextVersionNumber;

        var versionId = await db.QuerySingleAsync<int>(
            """
            INSERT INTO [RecipeComponentVersions]
                ([RecipeComponentId], [VersionNumber], [Status], [Instructions], [YieldQuantity], [YieldUnit],
                 [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                 [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [ShelfLifeHours],
                 [UseEarliestIngredientExpiry], [ChangeSummary], [IsTechnologyChange],
                 [NonTechnologyChangeReason], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@RecipeComponentId, @VersionNumber, @Status, @Instructions, @YieldQuantity, @YieldUnit,
                 @RawWeightGrams, @CookedWeightGrams, @CaloriesPer100g, @ProteinPer100g,
                 @CarbohydratesPer100g, @FatPer100g, @FiberPer100g, @ShelfLifeHours,
                 @UseEarliestIngredientExpiry, @ChangeSummary, @IsTechnologyChange,
                 @NonTechnologyChangeReason, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            version,
            tx);

        if (sourceVersionId.HasValue)
        {
            await db.ExecuteAsync(
                """
                INSERT INTO [RecipeComponentIngredients]
                    ([RecipeComponentVersionId], [IngredientId], [StockItemId], [WarehouseCategoryId],
                     [WeightInGrams], [YieldFactor], [IsOptional], [Notes], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    @versionId, [IngredientId], [StockItemId], [WarehouseCategoryId],
                    [WeightInGrams], [YieldFactor], [IsOptional], [Notes], @createdAt, @createdBy, 0
                FROM [RecipeComponentIngredients]
                WHERE [RecipeComponentVersionId] = @sourceVersionId
                  AND [IsDeleted] = 0;

                INSERT INTO [PackagingRequirements]
                    ([OwnerType], [MealId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                     [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                     [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    [OwnerType], NULL, @versionId, [StockItemId], [WarehouseCategoryId],
                    [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                    @createdAt, @createdBy, 0
                FROM [PackagingRequirements]
                WHERE [RecipeComponentVersionId] = @sourceVersionId
                  AND [IsDeleted] = 0;
                """,
                new
                {
                    versionId,
                    sourceVersionId,
                    createdAt = version.CreatedAt,
                    createdBy = version.CreatedBy,
                },
                tx);
        }

        tx.Commit();
        return versionId;
    }

    public async Task UpdateDraftVersionAsync(RecipeComponentVersion version)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [RecipeComponentVersions]
            SET [Instructions] = @Instructions,
                [YieldQuantity] = @YieldQuantity,
                [YieldUnit] = @YieldUnit,
                [RawWeightGrams] = @RawWeightGrams,
                [CookedWeightGrams] = @CookedWeightGrams,
                [CaloriesPer100g] = @CaloriesPer100g,
                [ProteinPer100g] = @ProteinPer100g,
                [CarbohydratesPer100g] = @CarbohydratesPer100g,
                [FatPer100g] = @FatPer100g,
                [FiberPer100g] = @FiberPer100g,
                [ShelfLifeHours] = @ShelfLifeHours,
                [UseEarliestIngredientExpiry] = @UseEarliestIngredientExpiry,
                [ChangeSummary] = @ChangeSummary,
                [IsTechnologyChange] = @IsTechnologyChange,
                [NonTechnologyChangeReason] = @NonTechnologyChangeReason,
                [UpdatedAt] = @UpdatedAt,
                [UpdatedBy] = @UpdatedBy
            WHERE [Id] = @Id
              AND [Status] = N'Draft'
              AND [IsDeleted] = 0;
            """,
            version);
    }

    public async Task UpdatePublishedNonTechnologyAsync(
        int versionId,
        string? instructions,
        string? changeSummary,
        string reason,
        string? updatedBy)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [RecipeComponentVersions]
            SET [Instructions] = @instructions,
                [ChangeSummary] = @changeSummary,
                [IsTechnologyChange] = 0,
                [NonTechnologyChangeReason] = @reason,
                [UpdatedAt] = @updatedAt,
                [UpdatedBy] = @updatedBy
            WHERE [Id] = @versionId
              AND [Status] = N'Published'
              AND [IsDeleted] = 0;
            """,
            new
            {
                versionId,
                instructions,
                changeSummary,
                reason,
                updatedAt = DateTimeOffset.UtcNow,
                updatedBy,
            });
    }

    public async Task<int> SaveIngredientAsync(RecipeComponentIngredient ingredient)
    {
        using var db = this.factory.CreateConnection();

        if (ingredient.Id > 0)
        {
            await db.ExecuteAsync(
                """
                UPDATE [RecipeComponentIngredients]
                SET [IngredientId] = @IngredientId,
                    [StockItemId] = @StockItemId,
                    [WarehouseCategoryId] = @WarehouseCategoryId,
                    [WeightInGrams] = @WeightInGrams,
                    [YieldFactor] = @YieldFactor,
                    [IsOptional] = @IsOptional,
                    [Notes] = @Notes,
                    [UpdatedAt] = @UpdatedAt,
                    [UpdatedBy] = @UpdatedBy
                WHERE [Id] = @Id
                  AND [IsDeleted] = 0;
                """,
                ingredient);

            return ingredient.Id;
        }

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [RecipeComponentIngredients]
                ([RecipeComponentVersionId], [IngredientId], [StockItemId], [WarehouseCategoryId],
                 [WeightInGrams], [YieldFactor], [IsOptional], [Notes], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@RecipeComponentVersionId, @IngredientId, @StockItemId, @WarehouseCategoryId,
                 @WeightInGrams, @YieldFactor, @IsOptional, @Notes, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ingredient);
    }

    public async Task DeleteIngredientAsync(int ingredientId, string? deletedBy)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [RecipeComponentIngredients]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [Id] = @ingredientId
              AND [RecipeComponentVersionId] IN (
                  SELECT [Id]
                  FROM [RecipeComponentVersions]
                  WHERE [Status] = N'Draft'
                    AND [IsDeleted] = 0
              );
            """,
            new { ingredientId, deletedAt = DateTimeOffset.UtcNow, deletedBy });
    }

    public async Task<int> SavePackagingAsync(PackagingRequirement packaging)
    {
        using var db = this.factory.CreateConnection();

        if (packaging.Id > 0)
        {
            await db.ExecuteAsync(
                """
                UPDATE [PackagingRequirements]
                SET [StockItemId] = @StockItemId,
                    [WarehouseCategoryId] = @WarehouseCategoryId,
                    [ResourceName] = @ResourceName,
                    [Quantity] = @Quantity,
                    [Unit] = @Unit,
                    [ContainerRole] = @ContainerRole,
                    [IsCustomerFacing] = @IsCustomerFacing,
                    [UpdatedAt] = @UpdatedAt,
                    [UpdatedBy] = @UpdatedBy
                WHERE [Id] = @Id
                  AND [IsDeleted] = 0;
                """,
                packaging);

            return packaging.Id;
        }

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@OwnerType, @MealId, @RecipeComponentVersionId, @StockItemId, @WarehouseCategoryId,
                 @ResourceName, @Quantity, @Unit, @ContainerRole, @IsCustomerFacing,
                 @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            packaging);
    }

    public async Task DeletePackagingAsync(int packagingRequirementId, string? deletedBy)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [PackagingRequirements]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [Id] = @packagingRequirementId
              AND [RecipeComponentVersionId] IN (
                  SELECT [Id]
                  FROM [RecipeComponentVersions]
                  WHERE [Status] = N'Draft'
                    AND [IsDeleted] = 0
              );
            """,
            new { packagingRequirementId, deletedAt = DateTimeOffset.UtcNow, deletedBy });
    }

    public async Task PublishVersionAsync(int versionId, string? publishedBy)
    {
        using var db = this.factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        var componentId = await db.ExecuteScalarAsync<int>(
            """
            SELECT [RecipeComponentId]
            FROM [RecipeComponentVersions]
            WHERE [Id] = @versionId
              AND [IsDeleted] = 0;
            """,
            new { versionId },
            tx);

        await db.ExecuteAsync(
            """
            UPDATE [RecipeComponentVersions]
            SET [Status] = N'Archived',
                [UpdatedAt] = @now,
                [UpdatedBy] = @publishedBy
            WHERE [RecipeComponentId] = @componentId
              AND [Status] = N'Published'
              AND [Id] <> @versionId
              AND [IsDeleted] = 0;

            UPDATE [RecipeComponentVersions]
            SET [Status] = N'Published',
                [PublishedAt] = @now,
                [PublishedBy] = @publishedBy,
                [UpdatedAt] = @now,
                [UpdatedBy] = @publishedBy
            WHERE [Id] = @versionId
              AND [IsDeleted] = 0;
            """,
            new
            {
                versionId,
                componentId,
                now = DateTimeOffset.UtcNow,
                publishedBy,
            },
            tx);

        tx.Commit();
    }

    public async Task AttachComponentToMealAsync(MealRecipeComponent component)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            INSERT INTO [MealRecipeComponents]
                ([MealId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit],
                 [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@MealId, @RecipeComponentVersionId, @Role, @QuantityPerServing, @Unit,
                 @SortOrder, @IsOptional, @CreatedAt, @CreatedBy, 0);
            """,
            component);
    }

    private static string VersionSelectSql()
    {
        return
            """
            SELECT
                rcv.[Id],
                rcv.[RecipeComponentId],
                rc.[Name] AS [ComponentName],
                rcv.[VersionNumber],
                rcv.[Status],
                rcv.[Instructions],
                rcv.[YieldQuantity],
                rcv.[YieldUnit],
                rcv.[RawWeightGrams],
                rcv.[CookedWeightGrams],
                rcv.[CaloriesPer100g],
                rcv.[ProteinPer100g],
                rcv.[CarbohydratesPer100g],
                rcv.[FatPer100g],
                rcv.[FiberPer100g],
                rcv.[ShelfLifeHours],
                rcv.[UseEarliestIngredientExpiry],
                rcv.[ChangeSummary],
                rcv.[IsTechnologyChange],
                rcv.[NonTechnologyChangeReason],
                rcv.[PublishedAt],
                rcv.[PublishedBy]
            FROM [RecipeComponentVersions] rcv
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            """;
    }
}
