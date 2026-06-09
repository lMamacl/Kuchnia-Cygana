using Dapper;
using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Adapters;

/// <summary>
/// Adapter IDietDataProvider oparty na encjach Modulu 2 (Menu).
/// Czyta opublikowane, datowane plany z DietMenuPlans/DietMenuPlanItems.
/// </summary>
public sealed class DietDataAdapter : IDietDataProvider
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMealVariantResultCalculator _resultCalculator;

    public DietDataAdapter(IDbConnectionFactory connectionFactory)
        : this(connectionFactory, new MealVariantResultCalculator())
    {
    }

    public DietDataAdapter(
        IDbConnectionFactory connectionFactory,
        IMealVariantResultCalculator resultCalculator)
    {
        _connectionFactory = connectionFactory;
        _resultCalculator = resultCalculator;
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

        var savedSnapshotItems = await GetSavedSnapshotItemsAsync(db, plan.DietMenuPlanId);
        if (savedSnapshotItems.Count > 0)
        {
            return new PublishedDietPlanSnapshotDto
            {
                DietMenuPlanId = plan.DietMenuPlanId,
                PlanDate = plan.PlanDate,
                PlanStatus = plan.PlanStatus,
                PublishedAt = plan.PublishedAt,
                PublishedBy = plan.PublishedBy,
                Items = savedSnapshotItems,
                Alerts = await GetPlanAlertsAsync(db, dateParam),
            };
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
                d.[Name] AS [DietName],
                dv.[Name] AS [DietVariantName],
                i.[MealVariantId],
                mv.[Name] AS [MealVariantName],
                mv.[VariantType] AS [MealVariantType],
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
                nf.[FiberPer100g],
                mv.[RawWeightGrams] AS [VariantRawWeightGrams],
                mv.[CookedWeightGrams] AS [VariantCookedWeightGrams],
                mv.[CaloriesPer100g] AS [VariantCaloriesPer100g],
                mv.[ProteinPer100g] AS [VariantProteinPer100g],
                mv.[CarbohydratesPer100g] AS [VariantCarbohydratesPer100g],
                mv.[FatPer100g] AS [VariantFatPer100g],
                mv.[FiberPer100g] AS [VariantFiberPer100g],
                mv.[NutritionSource] AS [VariantNutritionSource],
                mv.[NutritionOverrideReason] AS [VariantNutritionOverrideReason],
                COALESCE(mv.[AllergensApproved], CAST(0 AS bit)) AS [VariantAllergensApproved]
            FROM [DietMenuPlans] p
            INNER JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
            INNER JOIN [DietVariants] dv ON dv.[Id] = i.[DietVariantId] AND dv.[IsDeleted] = 0
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId] AND d.[IsDeleted] = 0
            INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
            LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId] AND mv.[IsDeleted] = 0
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
        var mealVariantIds = itemRows
            .Select(i => i.MealVariantId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
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
        var componentAllergensByVersion = await GetComponentAllergensByVersionAsync(db, componentVersionIds);
        var instructionSectionsByVersion = await GetInstructionSectionsByVersionAsync(db, componentVersionIds);
        var legacyIngredientsByMeal = await GetLegacyIngredientsByMealAsync(db, mealIds);
        var packagingByMeal = await GetPackagingByMealAsync(db, mealIds);
        var packagingByMealVariant = await GetPackagingByMealVariantAsync(db, mealVariantIds);
        var packagingByComponentVersion = await GetPackagingByComponentVersionAsync(db, componentVersionIds);
        var allergensByMealVariant = await GetAllergensByMealVariantAsync(db, mealVariantIds);
        var alerts = await GetPlanAlertsAsync(db, dateParam);

        var snapshotItems = new List<PublishedDietPlanItemDto>();
        foreach (var row in itemRows)
        {
            var components = BuildComponents(
                row,
                componentsByItem.GetValueOrDefault(row.DietMenuPlanItemId) ?? new List<ComponentRow>(),
                componentIngredientsByVersion,
                componentAllergensByVersion,
                instructionSectionsByVersion,
                legacyIngredientsByMeal,
                packagingByComponentVersion);
            var mealPackaging = packagingByMeal.GetValueOrDefault(row.MealId) ?? new List<PackagingRequirementDto>();
            var variantPackaging = new List<PackagingRequirementDto>();
            if (row.MealVariantId.HasValue
                && packagingByMealVariant.TryGetValue(row.MealVariantId.Value, out var foundVariantPackaging))
            {
                variantPackaging = foundVariantPackaging;
            }

            var result = _resultCalculator.Calculate(BuildCalculationRequest(
                row,
                components,
                mealPackaging,
                variantPackaging,
                allergensByMeal,
                allergensByMealVariant,
                componentAllergensByVersion));
            var projectedComponents = ApplyResultToComponents(components, result);

            snapshotItems.Add(new PublishedDietPlanItemDto
            {
                DietMenuPlanItemId = row.DietMenuPlanItemId,
                DietMenuPlanId = row.DietMenuPlanId,
                PlanDate = row.PlanDate,
                MealId = row.MealId,
                MealVariantId = row.MealVariantId,
                MealVariantName = row.MealVariantName,
                MealName = row.MealName,
                CategoryId = row.CategoryId,
                CategoryName = row.CategoryName,
                DietVariantId = row.DietVariantId,
                DietName = row.DietName,
                DietVariantName = row.DietVariantName,
                MealSlot = row.MealSlot,
                SortOrder = row.SortOrder,
                ServingMultiplier = row.ServingMultiplier,
                RawWeightGrams = result.FinalRawWeightGrams,
                CookedWeightGrams = result.FinalCookedWeightGrams,
                FinalWeightGrams = result.FinalWeightGrams,
                FinalWeightAfterMultiplierGrams = ScaleNullable(result.FinalWeightGrams, row.ServingMultiplier),
                NutritionSource = result.NutritionSource,
                NutritionOverrideReason = result.OverrideReason,
                ShelfLifeHours = row.ShelfLifeHours,
                UseEarliestIngredientExpiry = row.UseEarliestIngredientExpiry,
                Nutrition = MapNutrition(result, row.ServingMultiplier),
                Allergens = result.Allergens.Select(allergen => allergen.Name).ToList(),
                Components = projectedComponents,
                AggregateIngredients = result.Ingredients.Select(MapAggregateIngredient).ToList(),
                PackagingRequirements = result.PackagingRequirements.Select(MapPackagingRequirement).ToList(),
                RecipeComponentVersionIds = result.Components
                    .Select(component => component.RecipeComponentVersionId)
                    .Where(id => id > 0)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList(),
                ValidationWarnings = result.ValidationWarnings,
                CompletenessStatus = result.CompletenessStatus,
                IsAggregated = result.IsAggregated,
                IsCompleteForProduction = result.IsComplete,
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

    private static async Task<IReadOnlyList<PublishedDietPlanItemDto>> GetSavedSnapshotItemsAsync(
        System.Data.IDbConnection db,
        int dietMenuPlanId)
    {
        var rows = (await db.QueryAsync<SavedSnapshotItemRow>(
            """
            SELECT
                i.[PublishedSnapshotJson]
            FROM [DietMenuPlanItems] i
            WHERE i.[DietMenuPlanId] = @dietMenuPlanId
              AND i.[IsDeleted] = 0
              AND i.[IsActive] = 1
              AND i.[PublishedSnapshotJson] IS NOT NULL
              AND i.[PublishedSnapshotHash] IS NOT NULL
            ORDER BY i.[DietVariantId], i.[SortOrder], i.[Id];
            """,
            new { dietMenuPlanId })).ToList();

        if (rows.Count == 0)
        {
            return Array.Empty<PublishedDietPlanItemDto>();
        }

        var items = new List<PublishedDietPlanItemDto>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.PublishedSnapshotJson))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<PublishedDietPlanItemDto>(
                row.PublishedSnapshotJson,
                SnapshotJsonOptions);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static async Task<IReadOnlyList<PlanChangeAlertDto>> GetPlanAlertsAsync(
        System.Data.IDbConnection db,
        DateTime date)
    {
        var alerts = await db.QueryAsync<PlanChangeAlertDto>(
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
            new { date });

        return alerts.ToList();
    }

    public async Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate)
    {
        using var db = _connectionFactory.CreateConnection();

        var rows = await db.QueryAsync<DietPlanRow>(
            """
            SELECT
                p.[PlanDate],
                p.[Status] AS [PlanStatus],
                i.[Id] AS [DietMenuPlanItemId],
                i.[MealId],
                m.[Name] AS [MealName],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                i.[DietVariantId],
                i.[MealVariantId],
                NULL AS [MealVariantName],
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
                i.[Id] AS [DietMenuPlanItemId],
                i.[MealId],
                m.[Name] AS [MealName],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                i.[DietVariantId],
                i.[MealVariantId],
                NULL AS [MealVariantName],
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
                mrc.[IsOptional],
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
                rcv.[AllergensApproved]
            FROM [DietMenuPlanItems] i
            INNER JOIN (
                SELECT
                    i2.[Id] AS [DietMenuPlanItemId],
                    mrc.[Id],
                    mrc.[RecipeComponentVersionId],
                    mrc.[Role],
                    mrc.[QuantityPerServing],
                    mrc.[Unit],
                    mrc.[SortOrder],
                    mrc.[IsOptional],
                    mrc.[IsDeleted]
                FROM [DietMenuPlanItems] i2
                INNER JOIN [MealRecipeComponents] mrc ON mrc.[MealId] = i2.[MealId]
                WHERE i2.[MealVariantId] IS NULL

                UNION ALL

                SELECT
                    i2.[Id] AS [DietMenuPlanItemId],
                    mvc.[Id],
                    mvc.[RecipeComponentVersionId],
                    mvc.[Role],
                    mvc.[QuantityPerServing],
                    mvc.[Unit],
                    mvc.[SortOrder],
                    mvc.[IsOptional],
                    mvc.[IsDeleted]
                FROM [DietMenuPlanItems] i2
                INNER JOIN [MealVariantComponents] mvc ON mvc.[MealVariantId] = i2.[MealVariantId]
            ) mrc ON mrc.[DietMenuPlanItemId] = i.[Id]
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

    private static async Task<Dictionary<int, List<MealVariantResultAllergenInputDto>>> GetComponentAllergensByVersionAsync(
        System.Data.IDbConnection db,
        int[] componentVersionIds)
    {
        if (componentVersionIds.Length == 0)
        {
            return new Dictionary<int, List<MealVariantResultAllergenInputDto>>();
        }

        var rows = await db.QueryAsync<ComponentAllergenRow>(
            """
            SELECT
                rci.[RecipeComponentVersionId],
                a.[Id] AS [AllergenId],
                a.[Name],
                ia.[TraceAmount] AS [IsTrace],
                N'Ingredient' AS [SourceType],
                i.[Name] AS [SourceName]
            FROM [RecipeComponentIngredients] rci
            INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = i.[Id]
            INNER JOIN [Allergens] a ON a.[Id] = ia.[AllergenId]
            WHERE rci.[RecipeComponentVersionId] IN @componentVersionIds
              AND rci.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
              AND i.[IsActive] = 1
            ORDER BY rci.[RecipeComponentVersionId], a.[Name], i.[Name];
            """,
            new { componentVersionIds });

        return rows
            .GroupBy(row => row.RecipeComponentVersionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(MapAllergenInput).ToList());
    }

    private static async Task<Dictionary<int, List<MealVariantResultAllergenInputDto>>> GetAllergensByMealVariantAsync(
        System.Data.IDbConnection db,
        int[] mealVariantIds)
    {
        if (mealVariantIds.Length == 0)
        {
            return new Dictionary<int, List<MealVariantResultAllergenInputDto>>();
        }

        var rows = await db.QueryAsync<MealVariantAllergenRow>(
            """
            SELECT
                mva.[MealVariantId],
                a.[Id] AS [AllergenId],
                a.[Name],
                mva.[IsTrace],
                mva.[SourceType],
                mv.[Name] AS [SourceName]
            FROM [MealVariantAllergens] mva
            INNER JOIN [Allergens] a ON a.[Id] = mva.[AllergenId]
            INNER JOIN [MealVariants] mv ON mv.[Id] = mva.[MealVariantId]
            WHERE mva.[MealVariantId] IN @mealVariantIds
            ORDER BY mva.[MealVariantId], a.[Name];
            """,
            new { mealVariantIds });

        return rows
            .GroupBy(row => row.MealVariantId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(MapAllergenInput).ToList());
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

    private static async Task<Dictionary<int, List<ComponentInstructionSectionDto>>> GetInstructionSectionsByVersionAsync(
        System.Data.IDbConnection db,
        int[] componentVersionIds)
    {
        if (componentVersionIds.Length == 0)
        {
            return new Dictionary<int, List<ComponentInstructionSectionDto>>();
        }

        var sectionRows = (await db.QueryAsync<InstructionSectionRow>(
            """
            SELECT
                [Id],
                [RecipeComponentVersionId],
                [Title],
                [SortOrder]
            FROM [RecipeComponentInstructionSections]
            WHERE [RecipeComponentVersionId] IN @componentVersionIds
              AND [IsDeleted] = 0
            ORDER BY [RecipeComponentVersionId], [SortOrder], [Id];
            """,
            new { componentVersionIds })).ToList();

        var sectionIds = sectionRows.Select(s => s.Id).ToArray();
        var stepRows = sectionIds.Length == 0
            ? new List<InstructionStepRow>()
            : (await db.QueryAsync<InstructionStepRow>(
                """
                SELECT
                    [Id],
                    [RecipeComponentInstructionSectionId],
                    [StepText],
                    [SortOrder],
                    [RequiresControl],
                    [ControlType],
                    [ExpectedValue],
                    [ExpectedUnit],
                    [IsCritical]
                FROM [RecipeComponentInstructionSteps]
                WHERE [RecipeComponentInstructionSectionId] IN @sectionIds
                  AND [IsDeleted] = 0
                ORDER BY [RecipeComponentInstructionSectionId], [SortOrder], [Id];
                """,
                new { sectionIds })).ToList();

        var stepsBySection = stepRows
            .GroupBy(s => s.RecipeComponentInstructionSectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return sectionRows
            .GroupBy(s => s.RecipeComponentVersionId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(section => new ComponentInstructionSectionDto
                {
                    SectionId = section.Id,
                    Title = section.Title,
                    SortOrder = section.SortOrder,
                    Steps = (stepsBySection.GetValueOrDefault(section.Id) ?? new List<InstructionStepRow>())
                        .Select(step => new ComponentInstructionStepDto
                        {
                            StepId = step.Id,
                            StepText = step.StepText,
                            SortOrder = step.SortOrder,
                            RequiresControl = step.RequiresControl,
                            ControlType = step.ControlType,
                            ExpectedValue = step.ExpectedValue,
                            ExpectedUnit = step.ExpectedUnit,
                            IsCritical = step.IsCritical,
                        })
                        .ToList(),
                }).ToList());
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

    private static async Task<Dictionary<int, List<PackagingRequirementDto>>> GetPackagingByMealVariantAsync(
        System.Data.IDbConnection db,
        int[] mealVariantIds)
    {
        if (mealVariantIds.Length == 0)
        {
            return new Dictionary<int, List<PackagingRequirementDto>>();
        }

        var rows = await db.QueryAsync<PackagingRequirementRow>(
            """
            SELECT
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
            WHERE [MealVariantId] IN @mealVariantIds
              AND [IsDeleted] = 0
            ORDER BY [MealVariantId], [Id];
            """,
            new { mealVariantIds });

        return rows
            .Where(r => r.MealVariantId.HasValue)
            .GroupBy(r => r.MealVariantId!.Value)
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
        Dictionary<int, List<MealVariantResultAllergenInputDto>> componentAllergensByVersion,
        Dictionary<int, List<ComponentInstructionSectionDto>> instructionSectionsByVersion,
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
                    RawWeightGrams = item.RawWeightGrams,
                    CookedWeightGrams = item.CookedWeightGrams,
                    ShelfLifeHours = item.ShelfLifeHours,
                    UseEarliestIngredientExpiry = item.UseEarliestIngredientExpiry,
                    AllergensApproved = false,
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
            var instructionSections = instructionSectionsByVersion.GetValueOrDefault(row.RecipeComponentVersionId)
                ?? new List<ComponentInstructionSectionDto>();
            var packaging = packagingByComponentVersion.GetValueOrDefault(row.RecipeComponentVersionId)
                ?? new List<PackagingRequirementDto>();
            var allergens = componentAllergensByVersion.GetValueOrDefault(row.RecipeComponentVersionId)
                ?? new List<MealVariantResultAllergenInputDto>();
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
                IsOptional = row.IsOptional,
                Instructions = row.Instructions,
                YieldQuantity = row.YieldQuantity <= 0 ? 1.0m : row.YieldQuantity,
                YieldUnit = row.YieldUnit,
                RawWeightGrams = row.RawWeightGrams,
                CookedWeightGrams = row.CookedWeightGrams,
                Nutrition = MapComponentNutrition(row),
                ShelfLifeHours = row.ShelfLifeHours,
                UseEarliestIngredientExpiry = row.UseEarliestIngredientExpiry,
                AllergensApproved = row.AllergensApproved,
                Allergens = allergens.Select(allergen => allergen.Name).Distinct().OrderBy(name => name).ToList(),
                Ingredients = ingredients,
                PackagingRequirements = packaging,
                InstructionSections = instructionSections,
                ValidationWarnings = warnings,
                IsCompleteForProduction = warnings.Count == 0,
            };
        }).ToList();
    }

    private static MealVariantResultCalculationRequest BuildCalculationRequest(
        PublishedPlanItemRow row,
        IReadOnlyList<MealComponentVersionDto> components,
        IReadOnlyList<PackagingRequirementDto> mealPackaging,
        IReadOnlyList<PackagingRequirementDto> variantPackaging,
        Dictionary<int, IReadOnlyList<string>> allergensByMeal,
        Dictionary<int, List<MealVariantResultAllergenInputDto>> allergensByMealVariant,
        Dictionary<int, List<MealVariantResultAllergenInputDto>> componentAllergensByVersion)
    {
        var componentInputs = components.Select(component => MapComponentInput(component, componentAllergensByVersion)).ToList();

        return new MealVariantResultCalculationRequest
        {
            MealId = row.MealId,
            MealName = row.MealName,
            MealVariantId = row.MealVariantId,
            MealVariantName = row.MealVariantName,
            VariantType = row.MealVariantType,
            NutritionSource = row.MealVariantId.HasValue
                ? row.VariantNutritionSource ?? "Aggregated"
                : "Aggregated",
            OverrideReason = row.MealVariantId.HasValue ? row.VariantNutritionOverrideReason : null,
            ManualRawWeightGrams = row.MealVariantId.HasValue
                ? row.VariantRawWeightGrams ?? row.RawWeightGrams
                : row.RawWeightGrams,
            ManualCookedWeightGrams = row.MealVariantId.HasValue
                ? row.VariantCookedWeightGrams ?? row.CookedWeightGrams
                : row.CookedWeightGrams,
            ManualNutritionPer100g = row.MealVariantId.HasValue ? MapVariantNutrition(row) : null,
            AllergensApproved = row.MealVariantId.HasValue
                ? row.VariantAllergensApproved
                : componentInputs.All(component => component.AllergensApproved),
            BaseComponents = row.MealVariantId.HasValue
                ? Array.Empty<MealVariantResultComponentInputDto>()
                : componentInputs,
            VariantComponents = row.MealVariantId.HasValue
                ? componentInputs
                : Array.Empty<MealVariantResultComponentInputDto>(),
            MealPackagingRequirements = mealPackaging.Select(MapPackagingInput).ToList(),
            VariantPackagingRequirements = variantPackaging.Select(MapPackagingInput).ToList(),
            MealAllergens = (allergensByMeal.GetValueOrDefault(row.MealId) ?? Array.Empty<string>())
                .Select(name => new MealVariantResultAllergenInputDto
                {
                    Name = name,
                    SourceType = "Meal",
                })
                .ToList(),
            VariantAllergens = row.MealVariantId.HasValue
                ? allergensByMealVariant.GetValueOrDefault(row.MealVariantId.Value) ?? new List<MealVariantResultAllergenInputDto>()
                : Array.Empty<MealVariantResultAllergenInputDto>(),
        };
    }

    private static MealVariantResultComponentInputDto MapComponentInput(
        MealComponentVersionDto component,
        Dictionary<int, List<MealVariantResultAllergenInputDto>> componentAllergensByVersion)
    {
        return new MealVariantResultComponentInputDto
        {
            RecipeComponentId = component.RecipeComponentId,
            RecipeComponentVersionId = component.RecipeComponentVersionId,
            ComponentName = component.ComponentName,
            VersionNumber = component.VersionNumber,
            VersionStatus = component.VersionStatus,
            Role = component.Role,
            QuantityPerServing = component.QuantityPerServing,
            Unit = component.Unit,
            SortOrder = component.SortOrder,
            IsOptional = component.IsOptional,
            YieldQuantity = component.YieldQuantity <= 0 ? 1.0m : component.YieldQuantity,
            YieldUnit = string.IsNullOrWhiteSpace(component.YieldUnit) ? "portion" : component.YieldUnit,
            RawWeightGrams = component.RawWeightGrams,
            CookedWeightGrams = component.CookedWeightGrams,
            NutritionPer100g = MapNutritionInput(component.Nutrition),
            AllergensApproved = component.AllergensApproved,
            Ingredients = component.Ingredients.Select(MapIngredientInput).ToList(),
            PackagingRequirements = component.PackagingRequirements.Select(MapPackagingInput).ToList(),
            Allergens = componentAllergensByVersion.GetValueOrDefault(component.RecipeComponentVersionId)
                ?? new List<MealVariantResultAllergenInputDto>(),
        };
    }

    private static IReadOnlyList<MealComponentVersionDto> ApplyResultToComponents(
        IReadOnlyList<MealComponentVersionDto> components,
        MealVariantResultDto result)
    {
        var resultsByVersion = result.Components
            .GroupBy(component => component.RecipeComponentVersionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return components.Select(component =>
        {
            MealVariantResultComponentDto? resultComponent = null;
            if (resultsByVersion.TryGetValue(component.RecipeComponentVersionId, out var matching)
                && matching.Count > 0)
            {
                resultComponent = matching[0];
                matching.RemoveAt(0);
            }

            if (resultComponent is null)
            {
                return component;
            }

            component.RawWeightGrams = resultComponent.RawWeightGrams;
            component.CookedWeightGrams = resultComponent.CookedWeightGrams;
            component.FinalWeightGrams = resultComponent.FinalWeightGrams;
            component.ScaleFactor = resultComponent.ScaleFactor;
            component.Nutrition = MapNutrition(resultComponent.NutritionPer100g, resultComponent.NutritionPerServing);
            component.Allergens = resultComponent.Allergens.Select(allergen => allergen.Name).ToList();
            component.PackagingRequirements = resultComponent.PackagingRequirements.Select(MapPackagingRequirement).ToList();
            component.ValidationWarnings = resultComponent.ValidationWarnings;
            component.IsCompleteForProduction = resultComponent.IsComplete;
            return component;
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
            MealVariantId = row.MealVariantId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            ResourceName = row.ResourceName,
            Quantity = row.Quantity,
            Unit = row.Unit,
            ContainerRole = row.ContainerRole,
            IsCustomerFacing = row.IsCustomerFacing,
        };

    private static PackagingRequirementDto MapPackagingRequirement(MealVariantResultPackagingDto row)
        => new()
        {
            OwnerType = row.OwnerType,
            MealId = row.MealId,
            MealVariantId = row.MealVariantId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            ResourceName = row.ResourceName,
            Quantity = row.Quantity,
            Unit = row.Unit,
            ContainerRole = row.ContainerRole,
            IsCustomerFacing = row.IsCustomerFacing,
        };

    private static MealVariantResultPackagingInputDto MapPackagingInput(PackagingRequirementDto row)
        => new()
        {
            OwnerType = row.OwnerType,
            MealId = row.MealId,
            MealVariantId = row.MealVariantId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            ResourceName = row.ResourceName,
            Quantity = row.Quantity,
            Unit = row.Unit,
            ContainerRole = row.ContainerRole,
            IsCustomerFacing = row.IsCustomerFacing,
        };

    private static MealVariantResultIngredientInputDto MapIngredientInput(ComponentIngredientDto row)
        => new()
        {
            IngredientId = row.IngredientId,
            IngredientName = row.IngredientName,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            WeightInGrams = row.WeightInGrams,
            YieldFactor = row.YieldFactor <= 0 ? 1.0m : row.YieldFactor,
            IsOptional = row.IsOptional,
            Notes = row.Notes,
        };

    private static AggregateIngredientDto MapAggregateIngredient(MealVariantResultIngredientDto row)
        => new()
        {
            IngredientId = row.IngredientId,
            IngredientName = row.IngredientName,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            NetWeightInGrams = row.NetWeightInGrams,
            GrossWeightInGrams = row.GrossWeightInGrams,
            YieldFactor = row.YieldFactor,
            IsOptional = row.IsOptional,
            Notes = row.Notes,
            SourceRecipeComponentVersionIds = row.SourceRecipeComponentVersionIds,
            SourceComponentNames = row.SourceComponentNames,
        };

    private static MealVariantResultAllergenInputDto MapAllergenInput(ComponentAllergenRow row)
        => new()
        {
            AllergenId = row.AllergenId,
            Name = row.Name,
            IsTrace = row.IsTrace,
            SourceType = row.SourceType,
            SourceName = row.SourceName,
        };

    private static MealVariantResultAllergenInputDto MapAllergenInput(MealVariantAllergenRow row)
        => new()
        {
            AllergenId = row.AllergenId,
            Name = row.Name,
            IsTrace = row.IsTrace,
            SourceType = row.SourceType,
            SourceName = row.SourceName,
        };

    private static MealVariantNutritionDto? MapNutritionInput(LabelNutritionDto? nutrition)
        => nutrition is null
            ? null
            : new MealVariantNutritionDto
            {
                Calories = nutrition.CaloriesPer100g,
                Protein = nutrition.ProteinPer100g,
                Carbohydrates = nutrition.CarbohydratesPer100g,
                Fat = nutrition.FatPer100g,
                Fiber = nutrition.FiberPer100g,
            };

    private static MealVariantNutritionDto? MapVariantNutrition(PublishedPlanItemRow row)
    {
        if (!HasCompleteNutrition(
            row.VariantCaloriesPer100g,
            row.VariantProteinPer100g,
            row.VariantCarbohydratesPer100g,
            row.VariantFatPer100g,
            row.VariantFiberPer100g))
        {
            return null;
        }

        return new MealVariantNutritionDto
        {
            Calories = row.VariantCaloriesPer100g!.Value,
            Protein = row.VariantProteinPer100g!.Value,
            Carbohydrates = row.VariantCarbohydratesPer100g!.Value,
            Fat = row.VariantFatPer100g!.Value,
            Fiber = row.VariantFiberPer100g!.Value,
        };
    }

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

    private static LabelNutritionDto? MapNutrition(MealVariantResultDto result, decimal servingMultiplier)
        => MapNutrition(
            result.NutritionPer100g,
            result.NutritionPerServing is null
                ? null
                : ScaleNutrition(result.NutritionPerServing, servingMultiplier));

    private static LabelNutritionDto? MapNutrition(
        MealVariantNutritionDto? nutritionPer100g,
        MealVariantNutritionDto? nutritionPerServing)
    {
        if (nutritionPer100g is null)
        {
            return null;
        }

        return new LabelNutritionDto
        {
            CaloriesPer100g = nutritionPer100g.Calories,
            ProteinPer100g = nutritionPer100g.Protein,
            CarbohydratesPer100g = nutritionPer100g.Carbohydrates,
            FatPer100g = nutritionPer100g.Fat,
            FiberPer100g = nutritionPer100g.Fiber,
            CaloriesPerServing = nutritionPerServing?.Calories,
            ProteinPerServing = nutritionPerServing?.Protein,
            CarbohydratesPerServing = nutritionPerServing?.Carbohydrates,
            FatPerServing = nutritionPerServing?.Fat,
            FiberPerServing = nutritionPerServing?.Fiber,
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

    private static MealVariantNutritionDto ScaleNutrition(MealVariantNutritionDto nutrition, decimal factor)
        => new()
        {
            Calories = nutrition.Calories * factor,
            Protein = nutrition.Protein * factor,
            Carbohydrates = nutrition.Carbohydrates * factor,
            Fat = nutrition.Fat * factor,
            Fiber = nutrition.Fiber * factor,
        };

    private static decimal? ScaleNullable(decimal? value, decimal factor)
        => value.HasValue ? value.Value * factor : null;

    private static bool HasCompleteNutrition(params decimal?[] values)
        => values.All(value => value.HasValue);

    private static DietPlanEntry MapDietPlanRow(DietPlanRow row)
        => new()
        {
            PlanDate = row.PlanDate,
            PlanStatus = row.PlanStatus,
            DietMenuPlanItemId = row.DietMenuPlanItemId,
            MealId = row.MealId,
            MealVariantId = row.MealVariantId,
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

        public int? MealVariantId { get; set; }

        public string? MealVariantName { get; set; }

        public string? MealVariantType { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public int DietVariantId { get; set; }

        public string? DietName { get; set; }

        public string? DietVariantName { get; set; }

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

        public decimal? VariantRawWeightGrams { get; set; }

        public decimal? VariantCookedWeightGrams { get; set; }

        public decimal? VariantCaloriesPer100g { get; set; }

        public decimal? VariantProteinPer100g { get; set; }

        public decimal? VariantCarbohydratesPer100g { get; set; }

        public decimal? VariantFatPer100g { get; set; }

        public decimal? VariantFiberPer100g { get; set; }

        public string? VariantNutritionSource { get; set; }

        public string? VariantNutritionOverrideReason { get; set; }

        public bool VariantAllergensApproved { get; set; }
    }

    private sealed class SavedSnapshotItemRow
    {
        public string? PublishedSnapshotJson { get; set; }
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

        public bool IsOptional { get; set; }

        public string? Instructions { get; set; }

        public decimal YieldQuantity { get; set; }

        public string YieldUnit { get; set; } = "portion";

        public decimal? RawWeightGrams { get; set; }

        public decimal? CookedWeightGrams { get; set; }

        public decimal? CaloriesPer100g { get; set; }

        public decimal? ProteinPer100g { get; set; }

        public decimal? CarbohydratesPer100g { get; set; }

        public decimal? FatPer100g { get; set; }

        public decimal? FiberPer100g { get; set; }

        public int? ShelfLifeHours { get; set; }

        public bool UseEarliestIngredientExpiry { get; set; }

        public bool AllergensApproved { get; set; }
    }

    private sealed class InstructionSectionRow
    {
        public int Id { get; set; }

        public int RecipeComponentVersionId { get; set; }

        public string? Title { get; set; }

        public int SortOrder { get; set; }
    }

    private sealed class InstructionStepRow
    {
        public int Id { get; set; }

        public int RecipeComponentInstructionSectionId { get; set; }

        public string StepText { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool RequiresControl { get; set; }

        public string? ControlType { get; set; }

        public decimal? ExpectedValue { get; set; }

        public string? ExpectedUnit { get; set; }

        public bool IsCritical { get; set; }
    }

    private sealed class MealAllergenRow
    {
        public int MealId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ComponentAllergenRow
    {
        public int RecipeComponentVersionId { get; set; }

        public int? AllergenId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsTrace { get; set; }

        public string SourceType { get; set; } = string.Empty;

        public string? SourceName { get; set; }
    }

    private sealed class MealVariantAllergenRow
    {
        public int MealVariantId { get; set; }

        public int? AllergenId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsTrace { get; set; }

        public string SourceType { get; set; } = string.Empty;

        public string? SourceName { get; set; }
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

        public int? MealVariantId { get; set; }

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

        public int? DietMenuPlanItemId { get; set; }

        public int MealId { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public int DietVariantId { get; set; }

        public int? MealVariantId { get; set; }

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
