using Dapper;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Adapters;

/// <summary>
/// Adapter IDietDataProvider oparty na encjach Modulu 2 (Menu).
/// Czyta opublikowane, datowane plany z DietMenuPlans/DietMenuPlanItems.
/// </summary>
public sealed class DietDataAdapter : IDietDataProvider
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DietDataAdapter(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PublishedDietPlanSnapshotDto?> GetPublishedPlanSnapshotAsync(DateOnly date)
    {
        using var db = _connectionFactory.CreateConnection();
        var dateParam = date.ToDateTime(TimeOnly.MinValue);

        var plan = await db.QuerySingleOrDefaultAsync<PublishedPlanRow>(
            """
            SELECT TOP 1
                p.[Id] AS [DietMenuPlanId],
                p.[PlanDate],
                p.[Status] AS [PlanStatus],
                p.[PublishedAt],
                p.[PublishedBy]
            FROM [DietMenuPlans] p
            WHERE p.[PlanDate] = @date
              AND p.[Status] = N'Published'
              AND p.[IsDeleted] = 0;
            """,
            new { date = dateParam });

        if (plan is null)
        {
            return null;
        }

        var itemRows = (await db.QueryAsync<PublishedPlanItemRow>(
            """
            SELECT
                i.[Id] AS [DietMenuPlanItemId],
                p.[Id] AS [DietMenuPlanId],
                p.[PlanDate],
                i.[MealId],
                m.[Name] AS [MealName],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                i.[DietVariantId],
                i.[MealSlot],
                i.[SortOrder],
                i.[ServingSizeMultiplier] AS [ServingMultiplier],
                m.[RawWeightGrams],
                m.[CookedWeightGrams],
                m.[ShelfLifeHours],
                m.[UseEarliestIngredientExpiry],
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g]
            FROM [DietMenuPlans] p
            INNER JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
            INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            LEFT JOIN [NutritionFacts] nf ON nf.[MealId] = m.[Id]
            WHERE p.[Id] = @dietMenuPlanId
              AND i.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND m.[Status] IN (N'Published', N'Active')
            ORDER BY i.[DietVariantId], i.[SortOrder], i.[Id];
            """,
            new { plan.DietMenuPlanId })).ToList();

        var mealIds = itemRows.Select(i => i.MealId).Distinct().ToArray();
        var allergensByMeal = await GetAllergensByMealAsync(db, mealIds);
        var componentRows = (await QueryPublishedComponentRowsAsync(db, plan.DietMenuPlanId)).ToList();
        var componentsByItem = componentRows
            .GroupBy(c => c.DietMenuPlanItemId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var componentVersionIds = componentRows
            .Select(c => c.RecipeComponentVersionId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        var componentIngredientsByVersion = await GetComponentIngredientsByVersionAsync(db, componentVersionIds);
        var legacyIngredientsByMeal = await GetLegacyIngredientsByMealAsync(db, mealIds);
        var packagingByMeal = await GetPackagingByMealAsync(db, mealIds);
        var packagingByComponentVersion = await GetPackagingByComponentVersionAsync(db, componentVersionIds);
        var alerts = (await db.QueryAsync<PlanChangeAlertDto>(
            """
            SELECT
                [Id],
                [PlanDate],
                [DietMenuPlanId],
                [DietMenuPlanItemId],
                [MealId],
                [RecipeComponentVersionId],
                [AlertType],
                [Severity],
                [Message],
                [Reason],
                [RequiresAcknowledgement],
                [CreatedAt],
                [CreatedBy],
                [AcknowledgedAt],
                [AcknowledgedBy]
            FROM [PlanChangeAlerts]
            WHERE [PlanDate] = @date
            ORDER BY
                CASE WHEN [AcknowledgedAt] IS NULL THEN 0 ELSE 1 END,
                [CreatedAt] DESC,
                [Id] DESC;
            """,
            new { date = dateParam })).ToList();

        var snapshotItems = new List<PublishedDietPlanItemDto>();
        foreach (var row in itemRows)
        {
            var components = BuildComponents(
                row,
                componentsByItem.GetValueOrDefault(row.DietMenuPlanItemId) ?? new List<ComponentRow>(),
                componentIngredientsByVersion,
                legacyIngredientsByMeal,
                packagingByComponentVersion);
            var mealPackaging = packagingByMeal.GetValueOrDefault(row.MealId) ?? new List<PackagingRequirementDto>();
            var warnings = BuildItemWarnings(row, components, allergensByMeal, mealPackaging);

            snapshotItems.Add(new PublishedDietPlanItemDto
            {
                DietMenuPlanItemId = row.DietMenuPlanItemId,
                DietMenuPlanId = row.DietMenuPlanId,
                PlanDate = row.PlanDate,
                MealId = row.MealId,
                MealName = row.MealName,
                CategoryId = row.CategoryId,
                CategoryName = row.CategoryName,
                DietVariantId = row.DietVariantId,
                MealSlot = row.MealSlot,
                SortOrder = row.SortOrder,
                ServingMultiplier = row.ServingMultiplier,
                RawWeightGrams = row.RawWeightGrams,
                CookedWeightGrams = row.CookedWeightGrams,
                ShelfLifeHours = row.ShelfLifeHours,
                UseEarliestIngredientExpiry = row.UseEarliestIngredientExpiry,
                Nutrition = MapNutrition(row),
                Allergens = allergensByMeal.GetValueOrDefault(row.MealId) ?? Array.Empty<string>(),
                Components = components,
                PackagingRequirements = mealPackaging,
                ValidationWarnings = warnings,
                IsCompleteForProduction = warnings.Count == 0 && components.All(c => c.IsCompleteForProduction),
            });
        }

        return new PublishedDietPlanSnapshotDto
        {
            DietMenuPlanId = plan.DietMenuPlanId,
            PlanDate = plan.PlanDate,
            PlanStatus = plan.PlanStatus,
            PublishedAt = plan.PublishedAt,
            PublishedBy = plan.PublishedBy,
            Items = snapshotItems,
            Alerts = alerts,
        };
    }

    public async Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate)
    {
        using var db = _connectionFactory.CreateConnection();

        var rows = await db.QueryAsync<DietPlanRow>(
            """
            SELECT
                p.[PlanDate],
                p.[Status] AS [PlanStatus],
                i.[MealId],
                m.[Name] AS [MealName],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                i.[DietVariantId],
                i.[MealSlot],
                i.[SortOrder],
                i.[ServingSizeMultiplier] AS [ServingMultiplier]
            FROM [DietMenuPlans] p
            INNER JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
            INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            WHERE p.[PlanDate] >= @startDate
              AND p.[PlanDate] < @endDate
              AND p.[Status] = N'Published'
              AND p.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND m.[Status] IN (N'Published', N'Active')
            ORDER BY p.[PlanDate], i.[DietVariantId], i.[SortOrder], i.[Id];
            """,
            new
            {
                startDate = startDate.ToDateTime(TimeOnly.MinValue),
                endDate = startDate.AddDays(7).ToDateTime(TimeOnly.MinValue),
            });

        return rows.Select(MapDietPlanRow);
    }

    public async Task<IEnumerable<DietPlanEntry>> GetPlanForDateAsync(DateOnly date)
    {
        using var db = _connectionFactory.CreateConnection();

        var rows = await db.QueryAsync<DietPlanRow>(
            """
            SELECT
                p.[PlanDate],
                p.[Status] AS [PlanStatus],
                i.[MealId],
                m.[Name] AS [MealName],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                i.[DietVariantId],
                i.[MealSlot],
                i.[SortOrder],
                i.[ServingSizeMultiplier] AS [ServingMultiplier]
            FROM [DietMenuPlans] p
            INNER JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
            INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            WHERE p.[PlanDate] = @date
              AND p.[Status] = N'Published'
              AND p.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND m.[Status] IN (N'Published', N'Active')
            ORDER BY i.[DietVariantId], i.[SortOrder], i.[Id];
            """,
            new { date = date.ToDateTime(TimeOnly.MinValue) });

        return rows.Select(MapDietPlanRow);
    }

    public async Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId)
    {
        using var db = _connectionFactory.CreateConnection();

        var recipeRows = await db.QueryAsync<RecipeIngredientRow>(
            """
            SELECT
                r.[IngredientId],
                i.[Name] AS [IngredientName],
                COALESCE(i.[StockItemId], si.[Id]) AS [StockItemId],
                COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                wc.[Name] AS [WarehouseCategoryName],
                wc.[Code] AS [WarehouseCategoryCode],
                r.[WeightInGrams],
                i.[YieldFactor],
                i.[RequiresCoreTemperatureCheck],
                i.[MinimumCoreTemperatureCelsius],
                r.[IsOptional]
            FROM [Recipes] r
            INNER JOIN [Ingredients] i ON i.[Id] = r.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId])
            WHERE r.[MealId] = @mealId
              AND i.[IsActive] = 1
              AND i.[IsDeleted] = 0
            ORDER BY r.[Id];
            """,
            new { mealId });

        return recipeRows.Select(r => new RecipeIngredientEntry
        {
            IngredientId = r.IngredientId,
            IngredientName = r.IngredientName,
            StockItemId = r.StockItemId,
            WarehouseCategoryId = r.WarehouseCategoryId,
            WarehouseCategoryName = r.WarehouseCategoryName,
            WarehouseCategoryCode = r.WarehouseCategoryCode,
            WeightInGrams = r.WeightInGrams,
            YieldFactor = r.YieldFactor <= 0 ? 1.0m : r.YieldFactor,
            RequiresCoreTemperatureCheck = r.RequiresCoreTemperatureCheck,
            MinimumCoreTemperatureCelsius = r.MinimumCoreTemperatureCelsius,
            IsOptional = r.IsOptional,
        });
    }

    public async Task<MealCookingDetailsEntry?> GetMealCookingDetailsAsync(int mealId)
    {
        using var db = _connectionFactory.CreateConnection();

        var row = await db.QuerySingleOrDefaultAsync<MealCookingDetailsRow>(
            """
            SELECT TOP 1
                m.[Id] AS [MealId],
                m.[Name] AS [MealName],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                m.[Description],
                m.[PreparationInstructions],
                img.[Url] AS [MainImageUrl],
                m.[PreparationTimeMinutes],
                m.[RawWeightGrams],
                m.[CookedWeightGrams],
                m.[RequiresCoreTemperatureCheck],
                m.[MinimumCoreTemperatureCelsius],
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g]
            FROM [Meals] m
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            LEFT JOIN [NutritionFacts] nf ON nf.[MealId] = m.[Id]
            OUTER APPLY (
                SELECT TOP 1 mi.[Url]
                FROM [MealImages] mi
                WHERE mi.[MealId] = m.[Id]
                ORDER BY mi.[IsMain] DESC, mi.[Id] ASC
            ) img
            WHERE m.[Id] = @mealId
              AND m.[IsDeleted] = 0;
            """,
            new { mealId });

        if (row is null)
        {
            return null;
        }

        var allergens = (await db.QueryAsync<string>(
            """
            SELECT DISTINCT a.[Name]
            FROM [MealAllergens] ma
            INNER JOIN [Allergens] a ON a.[Id] = ma.[AllergenId]
            WHERE ma.[MealId] = @mealId
            UNION
            SELECT DISTINCT a.[Name]
            FROM [Recipes] r
            INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = r.[IngredientId]
            INNER JOIN [Allergens] a ON a.[Id] = ia.[AllergenId]
            WHERE r.[MealId] = @mealId
            ORDER BY [Name];
            """,
            new { mealId })).ToList();

        return new MealCookingDetailsEntry
        {
            MealId = row.MealId,
            MealName = row.MealName,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            Description = row.Description,
            PreparationInstructions = row.PreparationInstructions,
            MainImageUrl = row.MainImageUrl,
            PreparationTimeMinutes = row.PreparationTimeMinutes,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            RequiresCoreTemperatureCheck = row.RequiresCoreTemperatureCheck,
            MinimumCoreTemperatureCelsius = row.MinimumCoreTemperatureCelsius,
            NutritionFacts = row.CaloriesPer100g.HasValue
                ? new NutritionFactsEntry
                {
                    CaloriesPer100g = row.CaloriesPer100g.Value,
                    ProteinPer100g = row.ProteinPer100g.GetValueOrDefault(),
                    CarbohydratesPer100g = row.CarbohydratesPer100g.GetValueOrDefault(),
                    FatPer100g = row.FatPer100g.GetValueOrDefault(),
                    FiberPer100g = row.FiberPer100g.GetValueOrDefault(),
                }
                : null,
            Allergens = allergens,
        };
    }

    private static async Task<IEnumerable<ComponentRow>> QueryPublishedComponentRowsAsync(
        System.Data.IDbConnection db,
        int dietMenuPlanId)
    {
        return await db.QueryAsync<ComponentRow>(
            """
            SELECT
                i.[Id] AS [DietMenuPlanItemId],
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
                rcv.[YieldQuantity],
                rcv.[YieldUnit],
                rcv.[CookedWeightGrams],
                rcv.[CaloriesPer100g],
                rcv.[ProteinPer100g],
                rcv.[CarbohydratesPer100g],
                rcv.[FatPer100g],
                rcv.[FiberPer100g],
                rcv.[ShelfLifeHours],
                rcv.[UseEarliestIngredientExpiry]
            FROM [DietMenuPlanItems] i
            INNER JOIN [MealRecipeComponents] mrc ON mrc.[MealId] = i.[MealId]
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            WHERE i.[DietMenuPlanId] = @dietMenuPlanId
              AND i.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND mrc.[IsDeleted] = 0
              AND rcv.[IsDeleted] = 0
              AND rcv.[Status] IN (N'Published', N'Active')
              AND rc.[IsDeleted] = 0
              AND rc.[IsActive] = 1
            ORDER BY i.[Id], mrc.[SortOrder], mrc.[Id];
            """,
            new { dietMenuPlanId });
    }

    private static async Task<Dictionary<int, IReadOnlyList<string>>> GetAllergensByMealAsync(
        System.Data.IDbConnection db,
        int[] mealIds)
    {
        if (mealIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyList<string>>();
        }

        var rows = await db.QueryAsync<MealAllergenRow>(
            """
            SELECT DISTINCT ma.[MealId], a.[Name]
            FROM [MealAllergens] ma
            INNER JOIN [Allergens] a ON a.[Id] = ma.[AllergenId]
            WHERE ma.[MealId] IN @mealIds

            UNION

            SELECT DISTINCT r.[MealId], a.[Name]
            FROM [Recipes] r
            INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = r.[IngredientId]
            INNER JOIN [Allergens] a ON a.[Id] = ia.[AllergenId]
            WHERE r.[MealId] IN @mealIds

            UNION

            SELECT DISTINCT mrc.[MealId], a.[Name]
            FROM [MealRecipeComponents] mrc
            INNER JOIN [RecipeComponentIngredients] rci ON rci.[RecipeComponentVersionId] = mrc.[RecipeComponentVersionId]
            INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = rci.[IngredientId]
            INNER JOIN [Allergens] a ON a.[Id] = ia.[AllergenId]
            WHERE mrc.[MealId] IN @mealIds
              AND mrc.[IsDeleted] = 0
              AND rci.[IsDeleted] = 0
            ORDER BY [MealId], [Name];
            """,
            new { mealIds });

        return rows
            .GroupBy(r => r.MealId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(r => r.Name).Distinct().OrderBy(name => name).ToList());
    }

    private static async Task<Dictionary<int, List<ComponentIngredientDto>>> GetComponentIngredientsByVersionAsync(
        System.Data.IDbConnection db,
        int[] componentVersionIds)
    {
        if (componentVersionIds.Length == 0)
        {
            return new Dictionary<int, List<ComponentIngredientDto>>();
        }

        var rows = await db.QueryAsync<ComponentIngredientRowWithVersion>(
            """
            SELECT
                rci.[RecipeComponentVersionId],
                rci.[IngredientId],
                i.[Name] AS [IngredientName],
                COALESCE(rci.[StockItemId], i.[StockItemId], si.[Id]) AS [StockItemId],
                COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                wc.[Name] AS [WarehouseCategoryName],
                wc.[Code] AS [WarehouseCategoryCode],
                rci.[WeightInGrams],
                CASE
                    WHEN rci.[YieldFactor] <= 0 THEN i.[YieldFactor]
                    ELSE rci.[YieldFactor]
                END AS [YieldFactor],
                i.[RequiresCoreTemperatureCheck],
                i.[MinimumCoreTemperatureCelsius],
                rci.[IsOptional],
                rci.[Notes]
            FROM [RecipeComponentIngredients] rci
            INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId], si.[WarehouseCategoryId])
            WHERE rci.[RecipeComponentVersionId] IN @componentVersionIds
              AND rci.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND i.[IsDeleted] = 0
            ORDER BY rci.[RecipeComponentVersionId], rci.[Id];
            """,
            new { componentVersionIds });

        return rows
            .GroupBy(r => r.RecipeComponentVersionId)
            .ToDictionary(g => g.Key, g => g.Select(MapComponentIngredient).ToList());
    }

    private static async Task<Dictionary<int, List<ComponentIngredientDto>>> GetLegacyIngredientsByMealAsync(
        System.Data.IDbConnection db,
        int[] mealIds)
    {
        if (mealIds.Length == 0)
        {
            return new Dictionary<int, List<ComponentIngredientDto>>();
        }

        var rows = await db.QueryAsync<LegacyIngredientRow>(
            """
            SELECT
                r.[MealId],
                r.[IngredientId],
                i.[Name] AS [IngredientName],
                COALESCE(i.[StockItemId], si.[Id]) AS [StockItemId],
                COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                wc.[Name] AS [WarehouseCategoryName],
                wc.[Code] AS [WarehouseCategoryCode],
                r.[WeightInGrams],
                i.[YieldFactor],
                i.[RequiresCoreTemperatureCheck],
                i.[MinimumCoreTemperatureCelsius],
                r.[IsOptional],
                r.[Notes]
            FROM [Recipes] r
            INNER JOIN [Ingredients] i ON i.[Id] = r.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId])
            WHERE r.[MealId] IN @mealIds
              AND i.[IsActive] = 1
              AND i.[IsDeleted] = 0
            ORDER BY r.[MealId], r.[Id];
            """,
            new { mealIds });

        return rows
            .GroupBy(r => r.MealId)
            .ToDictionary(g => g.Key, g => g.Select(MapComponentIngredient).ToList());
    }

    private static async Task<Dictionary<int, List<PackagingRequirementDto>>> GetPackagingByMealAsync(
        System.Data.IDbConnection db,
        int[] mealIds)
    {
        if (mealIds.Length == 0)
        {
            return new Dictionary<int, List<PackagingRequirementDto>>();
        }

        var rows = await db.QueryAsync<PackagingRequirementRow>(
            """
            SELECT
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
            WHERE [MealId] IN @mealIds
              AND [IsDeleted] = 0
            ORDER BY [MealId], [Id];
            """,
            new { mealIds });

        return rows
            .Where(r => r.MealId.HasValue)
            .GroupBy(r => r.MealId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(MapPackagingRequirement).ToList());
    }

    private static async Task<Dictionary<int, List<PackagingRequirementDto>>> GetPackagingByComponentVersionAsync(
        System.Data.IDbConnection db,
        int[] componentVersionIds)
    {
        if (componentVersionIds.Length == 0)
        {
            return new Dictionary<int, List<PackagingRequirementDto>>();
        }

        var rows = await db.QueryAsync<PackagingRequirementRow>(
            """
            SELECT
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
            WHERE [RecipeComponentVersionId] IN @componentVersionIds
              AND [IsDeleted] = 0
            ORDER BY [RecipeComponentVersionId], [Id];
            """,
            new { componentVersionIds });

        return rows
            .Where(r => r.RecipeComponentVersionId.HasValue)
            .GroupBy(r => r.RecipeComponentVersionId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(MapPackagingRequirement).ToList());
    }

    private static IReadOnlyList<MealComponentVersionDto> BuildComponents(
        PublishedPlanItemRow item,
        List<ComponentRow> componentRows,
        Dictionary<int, List<ComponentIngredientDto>> componentIngredientsByVersion,
        Dictionary<int, List<ComponentIngredientDto>> legacyIngredientsByMeal,
        Dictionary<int, List<PackagingRequirementDto>> packagingByComponentVersion)
    {
        if (componentRows.Count == 0)
        {
            var legacyIngredients = legacyIngredientsByMeal.GetValueOrDefault(item.MealId)
                ?? new List<ComponentIngredientDto>();
            var warnings = BuildComponentWarnings(legacyIngredients, new List<PackagingRequirementDto>());

            return new[]
            {
                new MealComponentVersionDto
                {
                    RecipeComponentId = 0,
                    RecipeComponentVersionId = -item.MealId,
                    ComponentName = $"{item.MealName} (legacy Recipes)",
                    VersionNumber = 1,
                    VersionStatus = "Legacy",
                    Role = "legacy",
                    QuantityPerServing = 1.0m,
                    Unit = "portion",
                    SortOrder = 0,
                    ShelfLifeHours = item.ShelfLifeHours,
                    UseEarliestIngredientExpiry = item.UseEarliestIngredientExpiry,
                    Ingredients = legacyIngredients,
                    PackagingRequirements = Array.Empty<PackagingRequirementDto>(),
                    ValidationWarnings = warnings,
                    IsCompleteForProduction = warnings.Count == 0,
                },
            };
        }

        return componentRows.Select(row =>
        {
            var ingredients = componentIngredientsByVersion.GetValueOrDefault(row.RecipeComponentVersionId)
                ?? new List<ComponentIngredientDto>();
            var packaging = packagingByComponentVersion.GetValueOrDefault(row.RecipeComponentVersionId)
                ?? new List<PackagingRequirementDto>();
            var warnings = BuildComponentWarnings(ingredients, packaging);

            return new MealComponentVersionDto
            {
                RecipeComponentId = row.RecipeComponentId,
                RecipeComponentVersionId = row.RecipeComponentVersionId,
                ComponentName = row.ComponentName,
                VersionNumber = row.VersionNumber,
                VersionStatus = row.VersionStatus,
                Role = row.Role,
                QuantityPerServing = row.QuantityPerServing,
                Unit = row.Unit,
                SortOrder = row.SortOrder,
                Instructions = row.Instructions,
                YieldQuantity = row.YieldQuantity <= 0 ? 1.0m : row.YieldQuantity,
                YieldUnit = row.YieldUnit,
                Nutrition = MapComponentNutrition(row),
                ShelfLifeHours = row.ShelfLifeHours,
                UseEarliestIngredientExpiry = row.UseEarliestIngredientExpiry,
                Ingredients = ingredients,
                PackagingRequirements = packaging,
                ValidationWarnings = warnings,
                IsCompleteForProduction = warnings.Count == 0,
            };
        }).ToList();
    }

    private static List<string> BuildItemWarnings(
        PublishedPlanItemRow item,
        IReadOnlyList<MealComponentVersionDto> components,
        Dictionary<int, IReadOnlyList<string>> allergensByMeal,
        IReadOnlyList<PackagingRequirementDto> packaging)
    {
        var warnings = new List<string>();
        if (components.Count == 0)
        {
            warnings.Add("Brak wersjonowanych skladowych posilku.");
        }

        if (MapNutrition(item) is null)
        {
            warnings.Add("Brak nutrition posilku.");
        }

        if (!allergensByMeal.TryGetValue(item.MealId, out var allergens) || allergens.Count == 0)
        {
            warnings.Add("Brak alergenow posilku lub skladowych.");
        }

        if (packaging.Count == 0)
        {
            warnings.Add("Brak wymagan opakowan posilku.");
        }

        return warnings;
    }

    private static List<string> BuildComponentWarnings(
        IReadOnlyList<ComponentIngredientDto> ingredients,
        IReadOnlyList<PackagingRequirementDto> packaging)
    {
        var warnings = new List<string>();
        if (ingredients.Count == 0)
        {
            warnings.Add("Brak skladnikow skladowej.");
        }

        warnings.AddRange(ingredients
            .Where(i => !i.WarehouseCategoryId.HasValue)
            .Select(i => $"Brak kategorii magazynowej: {i.IngredientName}."));

        if (packaging.Count == 0)
        {
            warnings.Add("Brak wymagan opakowan skladowej.");
        }

        return warnings;
    }

    private static ComponentIngredientDto MapComponentIngredient(ComponentIngredientRow row)
        => new()
        {
            IngredientId = row.IngredientId,
            IngredientName = row.IngredientName,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            WarehouseCategoryCode = row.WarehouseCategoryCode,
            WeightInGrams = row.WeightInGrams,
            YieldFactor = row.YieldFactor <= 0 ? 1.0m : row.YieldFactor,
            RequiresCoreTemperatureCheck = row.RequiresCoreTemperatureCheck,
            MinimumCoreTemperatureCelsius = row.MinimumCoreTemperatureCelsius,
            IsOptional = row.IsOptional,
            Notes = row.Notes,
        };

    private static PackagingRequirementDto MapPackagingRequirement(PackagingRequirementRow row)
        => new()
        {
            OwnerType = row.OwnerType,
            MealId = row.MealId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            ResourceName = row.ResourceName,
            Quantity = row.Quantity,
            Unit = row.Unit,
            ContainerRole = row.ContainerRole,
            IsCustomerFacing = row.IsCustomerFacing,
        };

    private static LabelNutritionDto? MapNutrition(PublishedPlanItemRow row)
    {
        if (!row.CaloriesPer100g.HasValue)
        {
            return null;
        }

        var servingWeight = row.CookedWeightGrams ?? row.RawWeightGrams;
        var servingFactor = servingWeight.HasValue ? servingWeight.Value / 100m : (decimal?)null;

        return new LabelNutritionDto
        {
            CaloriesPer100g = row.CaloriesPer100g.Value,
            ProteinPer100g = row.ProteinPer100g.GetValueOrDefault(),
            CarbohydratesPer100g = row.CarbohydratesPer100g.GetValueOrDefault(),
            FatPer100g = row.FatPer100g.GetValueOrDefault(),
            FiberPer100g = row.FiberPer100g.GetValueOrDefault(),
            CaloriesPerServing = servingFactor * row.CaloriesPer100g.Value,
            ProteinPerServing = servingFactor * row.ProteinPer100g.GetValueOrDefault(),
            CarbohydratesPerServing = servingFactor * row.CarbohydratesPer100g.GetValueOrDefault(),
            FatPerServing = servingFactor * row.FatPer100g.GetValueOrDefault(),
            FiberPerServing = servingFactor * row.FiberPer100g.GetValueOrDefault(),
        };
    }

    private static LabelNutritionDto? MapComponentNutrition(ComponentRow row)
    {
        if (!row.CaloriesPer100g.HasValue)
        {
            return null;
        }

        var servingFactor = row.CookedWeightGrams.HasValue ? row.CookedWeightGrams.Value / 100m : (decimal?)null;

        return new LabelNutritionDto
        {
            CaloriesPer100g = row.CaloriesPer100g.Value,
            ProteinPer100g = row.ProteinPer100g.GetValueOrDefault(),
            CarbohydratesPer100g = row.CarbohydratesPer100g.GetValueOrDefault(),
            FatPer100g = row.FatPer100g.GetValueOrDefault(),
            FiberPer100g = row.FiberPer100g.GetValueOrDefault(),
            CaloriesPerServing = servingFactor * row.CaloriesPer100g.Value,
            ProteinPerServing = servingFactor * row.ProteinPer100g.GetValueOrDefault(),
            CarbohydratesPerServing = servingFactor * row.CarbohydratesPer100g.GetValueOrDefault(),
            FatPerServing = servingFactor * row.FatPer100g.GetValueOrDefault(),
            FiberPerServing = servingFactor * row.FiberPer100g.GetValueOrDefault(),
        };
    }

    private static DietPlanEntry MapDietPlanRow(DietPlanRow row)
        => new()
        {
            PlanDate = row.PlanDate,
            PlanStatus = row.PlanStatus,
            MealId = row.MealId,
            MealName = row.MealName,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            DietVariantId = row.DietVariantId,
            MealSlot = row.MealSlot,
            SortOrder = row.SortOrder,
            ServingMultiplier = row.ServingMultiplier,
            ServingWeightGrams = row.ServingMultiplier * 100m,
        };

    private sealed class PublishedPlanRow
    {
        public int DietMenuPlanId { get; set; }

        public DateOnly PlanDate { get; set; }

        public string PlanStatus { get; set; } = string.Empty;

        public DateTimeOffset? PublishedAt { get; set; }

        public string? PublishedBy { get; set; }
    }

    private sealed class PublishedPlanItemRow
    {
        public int DietMenuPlanItemId { get; set; }

        public int DietMenuPlanId { get; set; }

        public DateOnly PlanDate { get; set; }

        public int MealId { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public int DietVariantId { get; set; }

        public string MealSlot { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public decimal ServingMultiplier { get; set; }

        public decimal? RawWeightGrams { get; set; }

        public decimal? CookedWeightGrams { get; set; }

        public int? ShelfLifeHours { get; set; }

        public bool UseEarliestIngredientExpiry { get; set; }

        public decimal? CaloriesPer100g { get; set; }

        public decimal? ProteinPer100g { get; set; }

        public decimal? CarbohydratesPer100g { get; set; }

        public decimal? FatPer100g { get; set; }

        public decimal? FiberPer100g { get; set; }
    }

    private sealed class ComponentRow
    {
        public int DietMenuPlanItemId { get; set; }

        public int RecipeComponentId { get; set; }

        public int RecipeComponentVersionId { get; set; }

        public string ComponentName { get; set; } = string.Empty;

        public int VersionNumber { get; set; }

        public string VersionStatus { get; set; } = string.Empty;

        public string? Role { get; set; }

        public decimal QuantityPerServing { get; set; }

        public string Unit { get; set; } = "portion";

        public int SortOrder { get; set; }

        public string? Instructions { get; set; }

        public decimal YieldQuantity { get; set; }

        public string YieldUnit { get; set; } = "portion";

        public decimal? CookedWeightGrams { get; set; }

        public decimal? CaloriesPer100g { get; set; }

        public decimal? ProteinPer100g { get; set; }

        public decimal? CarbohydratesPer100g { get; set; }

        public decimal? FatPer100g { get; set; }

        public decimal? FiberPer100g { get; set; }

        public int? ShelfLifeHours { get; set; }

        public bool UseEarliestIngredientExpiry { get; set; }
    }

    private sealed class MealAllergenRow
    {
        public int MealId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private class ComponentIngredientRow
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public int? StockItemId { get; set; }

        public int? WarehouseCategoryId { get; set; }

        public string? WarehouseCategoryName { get; set; }

        public string? WarehouseCategoryCode { get; set; }

        public decimal WeightInGrams { get; set; }

        public decimal YieldFactor { get; set; }

        public bool RequiresCoreTemperatureCheck { get; set; }

        public decimal? MinimumCoreTemperatureCelsius { get; set; }

        public bool IsOptional { get; set; }

        public string? Notes { get; set; }
    }

    private sealed class ComponentIngredientRowWithVersion : ComponentIngredientRow
    {
        public int RecipeComponentVersionId { get; set; }
    }

    private sealed class LegacyIngredientRow : ComponentIngredientRow
    {
        public int MealId { get; set; }
    }

    private sealed class PackagingRequirementRow
    {
        public string OwnerType { get; set; } = string.Empty;

        public int? MealId { get; set; }

        public int? RecipeComponentVersionId { get; set; }

        public int? StockItemId { get; set; }

        public int? WarehouseCategoryId { get; set; }

        public string ResourceName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public string Unit { get; set; } = "pcs";

        public string? ContainerRole { get; set; }

        public bool IsCustomerFacing { get; set; }
    }

    private sealed class DietPlanRow
    {
        public DateOnly PlanDate { get; set; }

        public string PlanStatus { get; set; } = string.Empty;

        public int MealId { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public int DietVariantId { get; set; }

        public string MealSlot { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public decimal ServingMultiplier { get; set; }
    }

    private sealed class RecipeIngredientRow
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public int? StockItemId { get; set; }

        public int? WarehouseCategoryId { get; set; }

        public string? WarehouseCategoryName { get; set; }

        public string? WarehouseCategoryCode { get; set; }

        public decimal WeightInGrams { get; set; }

        public decimal YieldFactor { get; set; }

        public bool RequiresCoreTemperatureCheck { get; set; }

        public decimal? MinimumCoreTemperatureCelsius { get; set; }

        public bool IsOptional { get; set; }
    }

    private sealed class MealCookingDetailsRow
    {
        public int MealId { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public string? Description { get; set; }

        public string? PreparationInstructions { get; set; }

        public string? MainImageUrl { get; set; }

        public int PreparationTimeMinutes { get; set; }

        public decimal? RawWeightGrams { get; set; }

        public decimal? CookedWeightGrams { get; set; }

        public bool RequiresCoreTemperatureCheck { get; set; }

        public decimal? MinimumCoreTemperatureCelsius { get; set; }

        public decimal? CaloriesPer100g { get; set; }

        public decimal? ProteinPer100g { get; set; }

        public decimal? CarbohydratesPer100g { get; set; }

        public decimal? FatPer100g { get; set; }

        public decimal? FiberPer100g { get; set; }
    }
}
