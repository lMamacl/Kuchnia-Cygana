using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class MealRepository : BaseRepository<Meal>, IMealRepository
{
    public MealRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<MealSearchResult> SearchAsync(MealSearchQuery query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        using var db = this.Factory.CreateConnection();
        var (whereSql, parameters) = BuildSearchWhere(query);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            {MealRowsCte}
            SELECT COUNT(1)
            FROM MealRows
            WHERE {whereSql};
            """,
            parameters);

        var rows = await db.QueryAsync<MealListRow>(
            $"""
            {MealRowsCte}
            SELECT *
            FROM MealRows
            WHERE {whereSql}
            ORDER BY [Name], [Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new MealSearchResult
        {
            Items = rows.ToList(),
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<MealListRow>> SearchPlanningAsync(string? query, int limit)
    {
        var safeLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);
        var normalized = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        using var db = this.Factory.CreateConnection();
        var rows = await db.QueryAsync<MealListRow>(
            """
            SELECT TOP (@Limit)
                m.[Id],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                m.[Name],
                CONVERT(nvarchar(30), m.[Status]) AS [Status],
                m.[IsActive],
                effective.[RawWeightGrams],
                effective.[CookedWeightGrams],
                effective.[CaloriesPer100g],
                effective.[ProteinPer100g],
                effective.[CarbohydratesPer100g],
                effective.[FatPer100g],
                effective.[FiberPer100g],
                variantStats.[VariantCount],
                variantStats.[PublishedVariantCount],
                componentStatus.[ComponentCount],
                legacyComponentStats.[LegacyRecipeCount],
                packagingStats.[PackagingRequirementCount],
                nutritionStatus.[HasNutrition],
                packagingStatus.[MissingPackaging],
                publicationStatus.[MissingPublicationData]
            FROM [Meals] m
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            OUTER APPLY (
                SELECT TOP 1
                    mv.[Id],
                    mv.[RawWeightGrams],
                    mv.[CookedWeightGrams],
                    mv.[CaloriesPer100g],
                    mv.[ProteinPer100g],
                    mv.[CarbohydratesPer100g],
                    mv.[FatPer100g],
                    mv.[FiberPer100g],
                    mv.[AllergensApproved]
                FROM [MealVariants] mv
                WHERE mv.[MealId] = m.[Id]
                  AND mv.[IsDeleted] = 0
                ORDER BY
                    mv.[IsDefault] DESC,
                    CASE mv.[Status]
                        WHEN N'Published' THEN 0
                        WHEN N'Ready' THEN 1
                        WHEN N'Draft' THEN 2
                        ELSE 3
                    END,
                    mv.[Id]
            ) defaultVariant
            OUTER APPLY (
                SELECT TOP 1
                    nf.[CaloriesPer100g],
                    nf.[ProteinPer100g],
                    nf.[CarbohydratesPer100g],
                    nf.[FatPer100g],
                    nf.[FiberPer100g]
                FROM [NutritionFacts] nf
                WHERE nf.[MealId] = m.[Id]
                ORDER BY nf.[Id] DESC
            ) nutritionFacts
            OUTER APPLY (
                SELECT
                    COUNT(1) AS [VariantCount],
                    COALESCE(SUM(CASE WHEN mv.[Status] = N'Published' THEN 1 ELSE 0 END), 0) AS [PublishedVariantCount]
                FROM [MealVariants] mv
                WHERE mv.[MealId] = m.[Id]
                  AND mv.[IsDeleted] = 0
            ) variantStats
            OUTER APPLY (
                SELECT COUNT(1) AS [ComponentCount]
                FROM [MealRecipeComponents] mrc
                WHERE mrc.[MealId] = m.[Id]
                  AND mrc.[IsDeleted] = 0
            ) m2ComponentStats
            OUTER APPLY (
                SELECT COUNT(1) AS [LegacyRecipeCount]
                FROM [Recipes] r
                INNER JOIN [Ingredients] i ON i.[Id] = r.[IngredientId]
                WHERE r.[MealId] = m.[Id]
                  AND i.[IsActive] = 1
                  AND i.[IsDeleted] = 0
                  AND r.[IsDeleted] = 0
            ) legacyComponentStats
            OUTER APPLY (
                SELECT COUNT(1) AS [ComponentGapCount]
                FROM [MealRecipeComponents] mrc
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
                WHERE mrc.[MealId] = m.[Id]
                  AND mrc.[IsDeleted] = 0
                  AND rcv.[IsDeleted] = 0
                  AND rc.[IsDeleted] = 0
                  AND (
                        rcv.[Status] <> N'Published'
                        OR COALESCE(rcv.[CookedWeightGrams], rcv.[RawWeightGrams]) IS NULL
                        OR COALESCE(rcv.[CookedWeightGrams], rcv.[RawWeightGrams]) <= 0
                        OR rcv.[CaloriesPer100g] IS NULL
                        OR rcv.[ProteinPer100g] IS NULL
                        OR rcv.[CarbohydratesPer100g] IS NULL
                        OR rcv.[FatPer100g] IS NULL
                        OR rcv.[FiberPer100g] IS NULL
                        OR rcv.[AllergensApproved] = 0
                  )
            ) componentGapStats
            OUTER APPLY (
                SELECT COALESCE(NULLIF(m2ComponentStats.[ComponentCount], 0), legacyComponentStats.[LegacyRecipeCount], 0) AS [ComponentCount]
            ) componentStatus
            OUTER APPLY (
                SELECT COUNT(1) AS [PackagingRequirementCount]
                FROM [PackagingRequirements] pr
                WHERE pr.[IsDeleted] = 0
                  AND (
                        pr.[MealId] = m.[Id]
                        OR pr.[MealVariantId] IN (
                            SELECT mv.[Id]
                            FROM [MealVariants] mv
                            WHERE mv.[MealId] = m.[Id]
                              AND mv.[IsDeleted] = 0
                        )
                        OR pr.[RecipeComponentVersionId] IN (
                            SELECT mrc.[RecipeComponentVersionId]
                            FROM [MealRecipeComponents] mrc
                            WHERE mrc.[MealId] = m.[Id]
                              AND mrc.[IsDeleted] = 0
                        )
                  )
            ) packagingStats
            OUTER APPLY (
                SELECT
                    COALESCE(defaultVariant.[RawWeightGrams], m.[RawWeightGrams]) AS [RawWeightGrams],
                    COALESCE(defaultVariant.[CookedWeightGrams], m.[CookedWeightGrams]) AS [CookedWeightGrams],
                    COALESCE(defaultVariant.[CaloriesPer100g], nutritionFacts.[CaloriesPer100g]) AS [CaloriesPer100g],
                    COALESCE(defaultVariant.[ProteinPer100g], nutritionFacts.[ProteinPer100g]) AS [ProteinPer100g],
                    COALESCE(defaultVariant.[CarbohydratesPer100g], nutritionFacts.[CarbohydratesPer100g]) AS [CarbohydratesPer100g],
                    COALESCE(defaultVariant.[FatPer100g], nutritionFacts.[FatPer100g]) AS [FatPer100g],
                    COALESCE(defaultVariant.[FiberPer100g], nutritionFacts.[FiberPer100g]) AS [FiberPer100g]
            ) effective
            OUTER APPLY (
                SELECT CAST(CASE
                    WHEN effective.[CaloriesPer100g] IS NOT NULL
                     AND effective.[ProteinPer100g] IS NOT NULL
                     AND effective.[CarbohydratesPer100g] IS NOT NULL
                     AND effective.[FatPer100g] IS NOT NULL
                     AND effective.[FiberPer100g] IS NOT NULL
                    THEN 1 ELSE 0
                END AS bit) AS [HasNutrition]
            ) nutritionStatus
            OUTER APPLY (
                SELECT CAST(CASE WHEN packagingStats.[PackagingRequirementCount] = 0 THEN 1 ELSE 0 END AS bit) AS [MissingPackaging]
            ) packagingStatus
            OUTER APPLY (
                SELECT CAST(CASE
                    WHEN m.[IsActive] = 0
                      OR componentStatus.[ComponentCount] = 0
                      OR (m2ComponentStats.[ComponentCount] > 0 AND componentGapStats.[ComponentGapCount] > 0)
                      OR (m2ComponentStats.[ComponentCount] = 0 AND legacyComponentStats.[LegacyRecipeCount] > 0)
                      OR variantStats.[VariantCount] = 0
                      OR COALESCE(effective.[CookedWeightGrams], effective.[RawWeightGrams]) IS NULL
                      OR COALESCE(effective.[CookedWeightGrams], effective.[RawWeightGrams]) <= 0
                      OR nutritionStatus.[HasNutrition] = 0
                      OR packagingStatus.[MissingPackaging] = 1
                      OR (defaultVariant.[Id] IS NOT NULL AND defaultVariant.[AllergensApproved] = 0)
                    THEN 1 ELSE 0
                END AS bit) AS [MissingPublicationData]
            ) publicationStatus
            WHERE m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND m.[Status] IN (N'Published', N'Active')
              AND (@SearchPrefix IS NULL OR m.[Name] LIKE @SearchPrefix)
            ORDER BY m.[Name], m.[Id];
            """,
            new
            {
                Limit = safeLimit,
                SearchPrefix = normalized is null ? null : $"{normalized}%",
            });

        return rows.ToList();
    }

    public async Task<IEnumerable<Meal>> GetPublishedAsync()
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [Meals] WHERE [Status] = @Status AND [IsDeleted] = 0;";
        return await db.QueryAsync<Meal>(sql, new { Status = MealStatus.Published.ToString() });
    }

    public async Task<Meal?> GetWithRecipeAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        const string mealSql = "SELECT * FROM [Meals] WHERE [Id] = @Id AND [IsDeleted] = 0;";
        var meal = await db.QuerySingleOrDefaultAsync<Meal>(mealSql, new { Id = mealId });
        if (meal is null) return null;
        // recipes nie są używane w tej metodzie poza sprawdzeniem – można pominąć lub załadować osobno
        return meal;
    }

    public async Task<MealNutritionCost?> GetMealNutritionCostAsync(int mealId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [dbo].[fn_MealNutritionCost](@MealId);";
        return await db.QuerySingleOrDefaultAsync<MealNutritionCost>(sql, new { MealId = mealId });
    }

    private const string MealRowsCte = """
        WITH MealRows AS (
            SELECT
                m.[Id],
                m.[CategoryId],
                c.[Name] AS [CategoryName],
                m.[Name],
                m.[Description],
                m.[MarketingDescription],
                CONVERT(nvarchar(30), m.[Status]) AS [Status],
                m.[PreparationTimeMinutes],
                m.[IsActive],
                effective.[RawWeightGrams],
                effective.[CookedWeightGrams],
                effective.[CaloriesPer100g],
                effective.[ProteinPer100g],
                effective.[CarbohydratesPer100g],
                effective.[FatPer100g],
                effective.[FiberPer100g],
                variantStats.[VariantCount],
                variantStats.[PublishedVariantCount],
                componentStatus.[ComponentCount],
                packagingStats.[PackagingRequirementCount],
                allergenList.[AllergenNames],
                nutritionStatus.[HasNutrition],
                packagingStatus.[MissingPackaging],
                publicationStatus.[MissingPublicationData]
            FROM [Meals] m
            LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
            OUTER APPLY (
                SELECT TOP 1
                    mv.[Id],
                    mv.[RawWeightGrams],
                    mv.[CookedWeightGrams],
                    mv.[CaloriesPer100g],
                    mv.[ProteinPer100g],
                    mv.[CarbohydratesPer100g],
                    mv.[FatPer100g],
                    mv.[FiberPer100g],
                    mv.[AllergensApproved]
                FROM [MealVariants] mv
                WHERE mv.[MealId] = m.[Id]
                  AND mv.[IsDeleted] = 0
                ORDER BY
                    mv.[IsDefault] DESC,
                    CASE mv.[Status]
                        WHEN N'Published' THEN 0
                        WHEN N'Ready' THEN 1
                        WHEN N'Draft' THEN 2
                        ELSE 3
                    END,
                    mv.[Id]
            ) defaultVariant
            OUTER APPLY (
                SELECT TOP 1
                    nf.[CaloriesPer100g],
                    nf.[ProteinPer100g],
                    nf.[CarbohydratesPer100g],
                    nf.[FatPer100g],
                    nf.[FiberPer100g]
                FROM [NutritionFacts] nf
                WHERE nf.[MealId] = m.[Id]
                ORDER BY nf.[Id] DESC
            ) nutritionFacts
            OUTER APPLY (
                SELECT
                    COUNT(1) AS [VariantCount],
                    COALESCE(SUM(CASE WHEN mv.[Status] = N'Published' THEN 1 ELSE 0 END), 0) AS [PublishedVariantCount]
                FROM [MealVariants] mv
                WHERE mv.[MealId] = m.[Id]
                  AND mv.[IsDeleted] = 0
            ) variantStats
            OUTER APPLY (
                SELECT COUNT(1) AS [ComponentCount]
                FROM [MealRecipeComponents] mrc
                WHERE mrc.[MealId] = m.[Id]
                  AND mrc.[IsDeleted] = 0
            ) m2ComponentStats
            OUTER APPLY (
                SELECT COUNT(1) AS [LegacyComponentCount]
                FROM [Recipes] r
                INNER JOIN [Ingredients] i ON i.[Id] = r.[IngredientId]
                WHERE r.[MealId] = m.[Id]
                  AND i.[IsActive] = 1
                  AND i.[IsDeleted] = 0
            ) legacyComponentStats
            OUTER APPLY (
                SELECT COUNT(1) AS [ComponentGapCount]
                FROM [MealRecipeComponents] mrc
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
                INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
                WHERE mrc.[MealId] = m.[Id]
                  AND mrc.[IsDeleted] = 0
                  AND rcv.[IsDeleted] = 0
                  AND rc.[IsDeleted] = 0
                  AND (
                        rcv.[Status] <> N'Published'
                        OR COALESCE(rcv.[CookedWeightGrams], rcv.[RawWeightGrams]) IS NULL
                        OR COALESCE(rcv.[CookedWeightGrams], rcv.[RawWeightGrams]) <= 0
                        OR rcv.[CaloriesPer100g] IS NULL
                        OR rcv.[ProteinPer100g] IS NULL
                        OR rcv.[CarbohydratesPer100g] IS NULL
                        OR rcv.[FatPer100g] IS NULL
                        OR rcv.[FiberPer100g] IS NULL
                        OR rcv.[AllergensApproved] = 0
                        OR NOT EXISTS (
                            SELECT 1
                            FROM [RecipeComponentIngredients] rci
                            WHERE rci.[RecipeComponentVersionId] = rcv.[Id]
                              AND rci.[IsDeleted] = 0
                        )
                        OR NOT EXISTS (
                            SELECT 1
                            FROM [PackagingRequirements] pr
                            WHERE pr.[RecipeComponentVersionId] = rcv.[Id]
                              AND pr.[IsDeleted] = 0
                        )
                  )
            ) componentGapStats
            OUTER APPLY (
                SELECT COALESCE(NULLIF(m2ComponentStats.[ComponentCount], 0), legacyComponentStats.[LegacyComponentCount], 0) AS [ComponentCount]
            ) componentStatus
            OUTER APPLY (
                SELECT COUNT(1) AS [PackagingRequirementCount]
                FROM [PackagingRequirements] pr
                WHERE pr.[IsDeleted] = 0
                  AND (
                        pr.[MealId] = m.[Id]
                        OR pr.[MealVariantId] IN (
                            SELECT mv.[Id]
                            FROM [MealVariants] mv
                            WHERE mv.[MealId] = m.[Id]
                              AND mv.[IsDeleted] = 0
                        )
                        OR pr.[RecipeComponentVersionId] IN (
                            SELECT mrc.[RecipeComponentVersionId]
                            FROM [MealRecipeComponents] mrc
                            WHERE mrc.[MealId] = m.[Id]
                              AND mrc.[IsDeleted] = 0
                        )
                  )
            ) packagingStats
            OUTER APPLY (
                SELECT STUFF((
                    SELECT N', ' + allergens.[Name]
                    FROM (
                        SELECT DISTINCT a.[Name]
                        FROM [MealAllergens] ma
                        INNER JOIN [Allergens] a ON a.[Id] = ma.[AllergenId]
                        WHERE ma.[MealId] = m.[Id]
                        UNION
                        SELECT DISTINCT a.[Name]
                        FROM [MealVariantAllergens] mva
                        INNER JOIN [Allergens] a ON a.[Id] = mva.[AllergenId]
                        INNER JOIN [MealVariants] mv ON mv.[Id] = mva.[MealVariantId]
                        WHERE mv.[MealId] = m.[Id]
                          AND mv.[IsDeleted] = 0
                    ) allergens
                    ORDER BY allergens.[Name]
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 2, N'') AS [AllergenNames]
            ) allergenList
            OUTER APPLY (
                SELECT
                    COALESCE(defaultVariant.[RawWeightGrams], m.[RawWeightGrams]) AS [RawWeightGrams],
                    COALESCE(defaultVariant.[CookedWeightGrams], m.[CookedWeightGrams]) AS [CookedWeightGrams],
                    COALESCE(defaultVariant.[CaloriesPer100g], nutritionFacts.[CaloriesPer100g]) AS [CaloriesPer100g],
                    COALESCE(defaultVariant.[ProteinPer100g], nutritionFacts.[ProteinPer100g]) AS [ProteinPer100g],
                    COALESCE(defaultVariant.[CarbohydratesPer100g], nutritionFacts.[CarbohydratesPer100g]) AS [CarbohydratesPer100g],
                    COALESCE(defaultVariant.[FatPer100g], nutritionFacts.[FatPer100g]) AS [FatPer100g],
                    COALESCE(defaultVariant.[FiberPer100g], nutritionFacts.[FiberPer100g]) AS [FiberPer100g]
            ) effective
            OUTER APPLY (
                SELECT CAST(CASE
                    WHEN effective.[CaloriesPer100g] IS NOT NULL
                     AND effective.[ProteinPer100g] IS NOT NULL
                     AND effective.[CarbohydratesPer100g] IS NOT NULL
                     AND effective.[FatPer100g] IS NOT NULL
                     AND effective.[FiberPer100g] IS NOT NULL
                    THEN 1 ELSE 0
                END AS bit) AS [HasNutrition]
            ) nutritionStatus
            OUTER APPLY (
                SELECT CAST(CASE
                    WHEN packagingStats.[PackagingRequirementCount] = 0 THEN 1 ELSE 0
                END AS bit) AS [MissingPackaging]
            ) packagingStatus
            OUTER APPLY (
                SELECT CAST(CASE
                    WHEN m.[IsActive] = 0
                      OR componentStatus.[ComponentCount] = 0
                      OR (m2ComponentStats.[ComponentCount] > 0 AND componentGapStats.[ComponentGapCount] > 0)
                      OR (m2ComponentStats.[ComponentCount] = 0 AND legacyComponentStats.[LegacyComponentCount] > 0)
                      OR variantStats.[VariantCount] = 0
                      OR COALESCE(effective.[CookedWeightGrams], effective.[RawWeightGrams]) IS NULL
                      OR COALESCE(effective.[CookedWeightGrams], effective.[RawWeightGrams]) <= 0
                      OR nutritionStatus.[HasNutrition] = 0
                      OR packagingStatus.[MissingPackaging] = 1
                      OR (defaultVariant.[Id] IS NOT NULL AND defaultVariant.[AllergensApproved] = 0)
                    THEN 1 ELSE 0
                END AS bit) AS [MissingPublicationData]
            ) publicationStatus
            WHERE m.[IsDeleted] = 0
        )
        """;

    private static (string WhereSql, DynamicParameters Parameters) BuildSearchWhere(MealSearchQuery query)
    {
        var clauses = new List<string> { "1 = 1" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            clauses.Add(
                "([Name] LIKE @SearchLike OR [Description] LIKE @SearchLike OR [MarketingDescription] LIKE @SearchLike OR [CategoryName] LIKE @SearchLike)");
            parameters.Add("SearchLike", $"%{query.Search.Trim()}%");
        }

        if (query.CategoryId.HasValue)
        {
            clauses.Add("[CategoryId] = @CategoryId");
            parameters.Add("CategoryId", query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            clauses.Add("[Status] = @Status");
            parameters.Add("Status", query.Status.Trim());
        }

        if (query.PlanningEligibleOnly)
        {
            clauses.Add("[IsActive] = 1");
            clauses.Add("[Status] IN (N'Published', N'Active')");
        }

        if (query.AllergenId.HasValue)
        {
            clauses.Add(
                """
                (
                    EXISTS (
                        SELECT 1
                        FROM [MealAllergens] ma
                        WHERE ma.[MealId] = MealRows.[Id]
                          AND ma.[AllergenId] = @AllergenId
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM [MealVariantAllergens] mva
                        INNER JOIN [MealVariants] mv ON mv.[Id] = mva.[MealVariantId]
                        WHERE mv.[MealId] = MealRows.[Id]
                          AND mv.[IsDeleted] = 0
                          AND mva.[AllergenId] = @AllergenId
                    )
                )
                """);
            parameters.Add("AllergenId", query.AllergenId.Value);
        }

        if (query.MissingPublicationData)
        {
            clauses.Add("[MissingPublicationData] = 1");
        }

        if (query.HasVariants.HasValue)
        {
            clauses.Add(query.HasVariants.Value ? "[VariantCount] > 0" : "[VariantCount] = 0");
        }

        if (query.MissingPackaging)
        {
            clauses.Add("[MissingPackaging] = 1");
        }

        return (string.Join(" AND ", clauses), parameters);
    }
}


