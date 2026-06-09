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

    public async Task UpdateAsync(MealVariant variant)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [MealVariants]
            SET [Name] = @Name,
                [VariantType] = @VariantType,
                [Status] = @Status,
                [Description] = @Description,
                [RawWeightGrams] = @RawWeightGrams,
                [CookedWeightGrams] = @CookedWeightGrams,
                [CaloriesPer100g] = @CaloriesPer100g,
                [ProteinPer100g] = @ProteinPer100g,
                [CarbohydratesPer100g] = @CarbohydratesPer100g,
                [FatPer100g] = @FatPer100g,
                [FiberPer100g] = @FiberPer100g,
                [NutritionSource] = @NutritionSource,
                [NutritionOverrideReason] = @NutritionOverrideReason,
                [AllergensApproved] = @AllergensApproved,
                [AllergenOverrideReason] = @AllergenOverrideReason,
                [PublishedAt] = @PublishedAt,
                [PublishedBy] = @PublishedBy,
                [UpdatedAt] = @UpdatedAt,
                [UpdatedBy] = @UpdatedBy
            WHERE [Id] = @Id
              AND [IsDeleted] = 0;
            """,
            variant);
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
                mvc.[IsOptional],
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
                rcv.[AllergensApproved]
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

    public async Task<int> SaveComponentAsync(MealVariantComponent component)
    {
        using var db = this.factory.CreateConnection();

        if (component.Id > 0)
        {
            await db.ExecuteAsync(
                """
                UPDATE [MealVariantComponents]
                SET [RecipeComponentVersionId] = @RecipeComponentVersionId,
                    [Role] = @Role,
                    [QuantityPerServing] = @QuantityPerServing,
                    [Unit] = @Unit,
                    [SortOrder] = @SortOrder,
                    [IsOptional] = @IsOptional,
                    [UpdatedAt] = @UpdatedAt,
                    [UpdatedBy] = @UpdatedBy
                WHERE [Id] = @Id
                  AND [MealVariantId] = @MealVariantId
                  AND [IsDeleted] = 0;
                """,
                component);

            return component.Id;
        }

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [MealVariantComponents]
                ([MealVariantId], [RecipeComponentVersionId], [Role], [QuantityPerServing],
                 [Unit], [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@MealVariantId, @RecipeComponentVersionId, @Role, @QuantityPerServing,
                 @Unit, @SortOrder, @IsOptional, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            component);
    }

    public async Task DeleteComponentAsync(int componentId, string? deletedBy)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [MealVariantComponents]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [Id] = @componentId
              AND [IsDeleted] = 0;
            """,
            new { componentId, deletedAt = DateTimeOffset.UtcNow, deletedBy });
    }

    public async Task<IReadOnlyList<PackagingRequirementRow>> GetPackagingAsync(int mealVariantId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<PackagingRequirementRow>(
            """
            SELECT
                [Id],
                [OwnerType],
                [MealId],
                [MealVariantId],
                [RecipeComponentVersionId],
                [StockItemId],
                [WarehouseCategoryId],
                [ResourceName],
                [Quantity],
                [Unit],
                [ContainerRole],
                [IsCustomerFacing]
            FROM [PackagingRequirements]
            WHERE [MealVariantId] = @mealVariantId
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { mealVariantId });

        return rows.ToList();
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
                  AND [MealVariantId] = @MealVariantId
                  AND [IsDeleted] = 0;
                """,
                packaging);

            return packaging.Id;
        }

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [MealVariantId], [RecipeComponentVersionId],
                 [StockItemId], [WarehouseCategoryId], [ResourceName], [Quantity],
                 [Unit], [ContainerRole], [IsCustomerFacing], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@OwnerType, @MealId, @MealVariantId, @RecipeComponentVersionId,
                 @StockItemId, @WarehouseCategoryId, @ResourceName, @Quantity,
                 @Unit, @ContainerRole, @IsCustomerFacing, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            packaging);
    }

    public async Task DeletePackagingAsync(int mealVariantId, int packagingRequirementId, string? deletedBy)
    {
        using var db = this.factory.CreateConnection();

        await db.ExecuteAsync(
            """
            UPDATE [PackagingRequirements]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [Id] = @packagingRequirementId
              AND [MealVariantId] = @mealVariantId
              AND [IsDeleted] = 0;
            """,
            new { mealVariantId, packagingRequirementId, deletedAt = DateTimeOffset.UtcNow, deletedBy });
    }

    public async Task<IReadOnlyList<MealVariantAllergenRow>> GetAllergensAsync(int mealVariantId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<MealVariantAllergenRow>(
            """
            SELECT
                a.[Id] AS [AllergenId],
                a.[Name],
                mva.[IsTrace],
                mva.[SourceType],
                mv.[Name] AS [SourceName]
            FROM [MealVariantAllergens] mva
            INNER JOIN [Allergens] a ON a.[Id] = mva.[AllergenId]
            INNER JOIN [MealVariants] mv ON mv.[Id] = mva.[MealVariantId]
            WHERE mva.[MealVariantId] = @mealVariantId
            ORDER BY a.[Name];
            """,
            new { mealVariantId });

        return rows.ToList();
    }

    public async Task ReplaceAllergensAsync(int mealVariantId, IReadOnlyList<MealVariantAllergenRow> allergens)
    {
        using var db = this.factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        await db.ExecuteAsync(
            "DELETE FROM [MealVariantAllergens] WHERE [MealVariantId] = @mealVariantId;",
            new { mealVariantId },
            tx);

        foreach (var allergen in allergens.Where(allergen => allergen.AllergenId.HasValue))
        {
            await db.ExecuteAsync(
                """
                INSERT INTO [MealVariantAllergens]
                    ([MealVariantId], [AllergenId], [IsTrace], [SourceType])
                VALUES
                    (@mealVariantId, @allergenId, @isTrace, @sourceType);
                """,
                new
                {
                    mealVariantId,
                    allergenId = allergen.AllergenId!.Value,
                    isTrace = allergen.IsTrace,
                    sourceType = string.IsNullOrWhiteSpace(allergen.SourceType) ? "Aggregated" : allergen.SourceType,
                },
                tx);
        }

        tx.Commit();
    }
}
