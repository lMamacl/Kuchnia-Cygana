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
