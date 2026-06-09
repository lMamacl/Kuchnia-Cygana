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

    public async Task<RecipeComponentSearchResult> SearchComponentsAsync(RecipeComponentSearchQuery query)
    {
        using var db = this.factory.CreateConnection();
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100);
        var (whereSql, parameters) = BuildComponentSearchWhere(query);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        const string componentRowsSql = """
            WITH LatestVersions AS (
                SELECT
                    rcv.[RecipeComponentId],
                    rcv.[Id],
                    rcv.[VersionNumber],
                    rcv.[Status],
                    rcv.[CaloriesPer100g],
                    rcv.[ProteinPer100g],
                    rcv.[CarbohydratesPer100g],
                    rcv.[FatPer100g],
                    rcv.[FiberPer100g],
                    rcv.[AllergensApproved],
                    ROW_NUMBER() OVER (
                        PARTITION BY rcv.[RecipeComponentId]
                        ORDER BY rcv.[VersionNumber] DESC, rcv.[Id] DESC
                    ) AS [RowNumber]
                FROM [RecipeComponentVersions] rcv
                WHERE rcv.[IsDeleted] = 0
            ),
            ComponentRows AS (
                SELECT
                    rc.[Id],
                    rc.[CategoryId],
                    c.[Name] AS [CategoryName],
                    rc.[Name],
                    rc.[Description],
                    rc.[ImageUrl],
                    rc.[PreparationTimeMinutes],
                    rc.[IsActive],
                    COUNT(rcv.[Id]) AS [VersionCount],
                    lv.[Id] AS [LatestVersionId],
                    lv.[VersionNumber] AS [LatestVersionNumber],
                    lv.[Status] AS [LatestVersionStatus],
                    CAST(CASE WHEN validation.[GapCount] > 0 THEN 1 ELSE 0 END AS bit) AS [HasPublicationGaps],
                    validation.[GapCount] AS [PublicationGapCount]
                FROM [RecipeComponents] rc
                LEFT JOIN [Categories] c ON c.[Id] = rc.[CategoryId]
                LEFT JOIN [RecipeComponentVersions] rcv ON rcv.[RecipeComponentId] = rc.[Id] AND rcv.[IsDeleted] = 0
                LEFT JOIN LatestVersions lv ON lv.[RecipeComponentId] = rc.[Id] AND lv.[RowNumber] = 1
                OUTER APPLY (
                    SELECT
                        COUNT(rci.[Id]) AS [IngredientCount],
                        SUM(CASE
                            WHEN COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId], si.[WarehouseCategoryId]) IS NULL THEN 1
                            ELSE 0
                        END) AS [MissingWarehouseCategoryCount]
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
                    LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
                    WHERE rci.[RecipeComponentVersionId] = lv.[Id]
                      AND rci.[IsDeleted] = 0
                      AND i.[IsDeleted] = 0
                ) ingredientStats
                OUTER APPLY (
                    SELECT COUNT(pr.[Id]) AS [PackagingCount]
                    FROM [PackagingRequirements] pr
                    WHERE pr.[RecipeComponentVersionId] = lv.[Id]
                      AND pr.[IsDeleted] = 0
                ) packagingStats
                OUTER APPLY (
                    SELECT COUNT(steps.[Id]) AS [StepCount]
                    FROM [RecipeComponentInstructionSections] sections
                    INNER JOIN [RecipeComponentInstructionSteps] steps
                        ON steps.[RecipeComponentInstructionSectionId] = sections.[Id]
                       AND steps.[IsDeleted] = 0
                    WHERE sections.[RecipeComponentVersionId] = lv.[Id]
                      AND sections.[IsDeleted] = 0
                ) instructionStats
                OUTER APPLY (
                    SELECT
                        (CASE WHEN lv.[Id] IS NULL THEN 1 ELSE 0 END)
                      + (CASE WHEN lv.[Id] IS NOT NULL AND (
                                lv.[CaloriesPer100g] IS NULL
                             OR lv.[ProteinPer100g] IS NULL
                             OR lv.[CarbohydratesPer100g] IS NULL
                             OR lv.[FatPer100g] IS NULL
                             OR lv.[FiberPer100g] IS NULL
                            ) THEN 1 ELSE 0 END)
                      + (CASE WHEN lv.[Id] IS NOT NULL AND COALESCE(ingredientStats.[IngredientCount], 0) = 0 THEN 1 ELSE 0 END)
                      + (CASE WHEN lv.[Id] IS NOT NULL AND COALESCE(ingredientStats.[MissingWarehouseCategoryCount], 0) > 0 THEN 1 ELSE 0 END)
                      + (CASE WHEN lv.[Id] IS NOT NULL AND COALESCE(packagingStats.[PackagingCount], 0) = 0 THEN 1 ELSE 0 END)
                      + (CASE WHEN lv.[Id] IS NOT NULL AND lv.[AllergensApproved] = 0 THEN 1 ELSE 0 END)
                      + (CASE WHEN lv.[Id] IS NOT NULL AND COALESCE(instructionStats.[StepCount], 0) = 0 THEN 1 ELSE 0 END)
                        AS [GapCount]
                ) validation
                WHERE rc.[IsDeleted] = 0
                GROUP BY
                    rc.[Id],
                    rc.[CategoryId],
                    c.[Name],
                    rc.[Name],
                    rc.[Description],
                    rc.[ImageUrl],
                    rc.[PreparationTimeMinutes],
                    rc.[IsActive],
                    lv.[Id],
                    lv.[VersionNumber],
                    lv.[Status],
                    validation.[GapCount]
            )
            """;

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"{componentRowsSql} SELECT COUNT(1) FROM ComponentRows cr WHERE {whereSql};",
            parameters);

        var rows = await db.QueryAsync<RecipeComponentListRow>(
            $"""
            {componentRowsSql}
            SELECT
                cr.[Id],
                cr.[CategoryId],
                cr.[CategoryName],
                cr.[Name],
                cr.[Description],
                cr.[ImageUrl],
                cr.[PreparationTimeMinutes],
                cr.[IsActive],
                cr.[VersionCount],
                cr.[LatestVersionId],
                cr.[LatestVersionNumber],
                cr.[LatestVersionStatus],
                cr.[HasPublicationGaps],
                cr.[PublicationGapCount]
            FROM ComponentRows cr
            WHERE {whereSql}
            ORDER BY cr.[Name], cr.[Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new RecipeComponentSearchResult
        {
            Items = rows.ToList(),
            TotalCount = totalCount,
        };
    }

    public async Task<RecipeComponentDetailRow?> GetComponentAsync(int componentId)
    {
        using var db = this.factory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<RecipeComponentDetailRow>(
            """
            SELECT
                rc.[Id],
                rc.[CategoryId],
                c.[Name] AS [CategoryName],
                rc.[Name],
                rc.[Description],
                rc.[ImageUrl],
                rc.[PreparationTimeMinutes],
                rc.[IsActive]
            FROM [RecipeComponents] rc
            LEFT JOIN [Categories] c ON c.[Id] = rc.[CategoryId]
            WHERE rc.[Id] = @componentId
              AND rc.[IsDeleted] = 0;
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
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g],
                rci.[IsOptional],
                rci.[Notes]
            FROM [RecipeComponentIngredients] rci
            INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId])
            OUTER APPLY (
                SELECT TOP 1
                    facts.[CaloriesPer100g],
                    facts.[ProteinPer100g],
                    facts.[CarbohydratesPer100g],
                    facts.[FatPer100g],
                    facts.[FiberPer100g]
                FROM [NutritionFacts] facts
                WHERE facts.[IngredientId] = i.[Id]
                ORDER BY facts.[Id] DESC
            ) nf
            WHERE rci.[RecipeComponentVersionId] = @versionId
              AND rci.[IsDeleted] = 0
            ORDER BY rci.[Id];
            """,
            new { versionId });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<RecipeComponentAllergenRow>> GetVersionAllergensAsync(int versionId)
    {
        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<RecipeComponentAllergenRow>(
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
            WHERE rci.[RecipeComponentVersionId] = @versionId
              AND rci.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
            ORDER BY a.[Name], i.[Name];
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
            WHERE [RecipeComponentVersionId] = @versionId
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { versionId });

        return rows.ToList();
    }

    public async Task<RecipeComponentVersionDetailsBulkRow> GetVersionDetailsBulkAsync(IEnumerable<int> versionIds)
    {
        var ids = versionIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return RecipeComponentVersionDetailsBulkRow.Empty;
        }

        using var db = this.factory.CreateConnection();

        var ingredients = (await db.QueryAsync<RecipeComponentIngredientRow>(
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
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g],
                rci.[IsOptional],
                rci.[Notes]
            FROM [RecipeComponentIngredients] rci
            INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId])
            OUTER APPLY (
                SELECT TOP 1
                    facts.[CaloriesPer100g],
                    facts.[ProteinPer100g],
                    facts.[CarbohydratesPer100g],
                    facts.[FatPer100g],
                    facts.[FiberPer100g]
                FROM [NutritionFacts] facts
                WHERE facts.[IngredientId] = i.[Id]
                ORDER BY facts.[Id] DESC
            ) nf
            WHERE rci.[RecipeComponentVersionId] IN @versionIds
              AND rci.[IsDeleted] = 0
            ORDER BY rci.[RecipeComponentVersionId], rci.[Id];
            """,
            new { versionIds = ids })).ToList();

        var packaging = (await db.QueryAsync<PackagingRequirementRow>(
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
            WHERE [RecipeComponentVersionId] IN @versionIds
              AND [IsDeleted] = 0
            ORDER BY [RecipeComponentVersionId], [Id];
            """,
            new { versionIds = ids })).ToList();

        var allergens = (await db.QueryAsync<RecipeComponentAllergenRow>(
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
            WHERE rci.[RecipeComponentVersionId] IN @versionIds
              AND rci.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
            ORDER BY rci.[RecipeComponentVersionId], a.[Name], i.[Name];
            """,
            new { versionIds = ids })).ToList();

        return new RecipeComponentVersionDetailsBulkRow
        {
            IngredientsByVersionId = ingredients
                .GroupBy(row => row.RecipeComponentVersionId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<RecipeComponentIngredientRow>)group.ToList()),
            PackagingByVersionId = packaging
                .Where(row => row.RecipeComponentVersionId.HasValue)
                .GroupBy(row => row.RecipeComponentVersionId!.Value)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<PackagingRequirementRow>)group.ToList()),
            AllergensByVersionId = allergens
                .GroupBy(row => row.RecipeComponentVersionId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<RecipeComponentAllergenRow>)group.ToList()),
        };
    }

    public async Task<IReadOnlyList<PackagingRequirementRow>> GetMealPackagingAsync(int mealId)
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
            WHERE [MealId] = @mealId
              AND [MealVariantId] IS NULL
              AND [RecipeComponentVersionId] IS NULL
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { mealId });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<RecipeComponentInstructionSectionRow>> GetVersionInstructionSectionsAsync(int versionId)
    {
        using var db = this.factory.CreateConnection();

        var sections = (await db.QueryAsync<RecipeComponentInstructionSectionRow>(
            """
            SELECT
                [Id],
                [RecipeComponentVersionId],
                [Title],
                [SortOrder]
            FROM [RecipeComponentInstructionSections]
            WHERE [RecipeComponentVersionId] = @versionId
              AND [IsDeleted] = 0
            ORDER BY [SortOrder], [Id];
            """,
            new { versionId })).ToList();

        var sectionIds = sections.Select(s => s.Id).ToArray();
        if (sectionIds.Length == 0)
        {
            return sections;
        }

        var steps = (await db.QueryAsync<RecipeComponentInstructionStepRow>(
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

        var stepsBySection = steps
            .GroupBy(s => s.RecipeComponentInstructionSectionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RecipeComponentInstructionStepRow>)g.ToList());

        foreach (var section in sections)
        {
            section.Steps = stepsBySection.GetValueOrDefault(section.Id) ?? Array.Empty<RecipeComponentInstructionStepRow>();
        }

        return sections;
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

    public async Task<IReadOnlyList<RecipeComponentVersionOptionRow>> SearchPublishedVersionOptionsAsync(string? query, int limit)
    {
        var safeLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 20);
        var normalized = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        using var db = this.factory.CreateConnection();

        var rows = await db.QueryAsync<RecipeComponentVersionOptionRow>(
            """
            SELECT TOP (@Limit)
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
              AND (@SearchPrefix IS NULL OR rc.[Name] LIKE @SearchPrefix)
            ORDER BY rc.[Name], rcv.[VersionNumber] DESC;
            """,
            new
            {
                Limit = safeLimit,
                SearchPrefix = normalized is null ? null : $"{normalized}%",
            });

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
                rcv.[AllergensApproved],
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
                CAST(1.0 AS decimal(10, 3)) AS [YieldQuantity],
                N'portion' AS [YieldUnit],
                m.[RawWeightGrams],
                m.[CookedWeightGrams],
                NULL AS [CaloriesPer100g],
                NULL AS [ProteinPer100g],
                NULL AS [CarbohydratesPer100g],
                NULL AS [FatPer100g],
                NULL AS [FiberPer100g],
                m.[ShelfLifeHours],
                m.[UseEarliestIngredientExpiry],
                CAST(0 AS bit) AS [AllergensApproved],
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
                ([CategoryId], [Name], [Description], [ImageUrl], [PreparationTimeMinutes],
                 [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@CategoryId, @Name, @Description, @ImageUrl, @PreparationTimeMinutes,
                 @IsActive, @CreatedAt, @CreatedBy, 0);
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
                 [UseEarliestIngredientExpiry], [NutritionSource], [NutritionOverrideReason],
                 [AllergensApproved], [AllergenOverrideReason], [AllergensApprovedAt],
                 [AllergensApprovedBy], [ChangeSummary], [IsTechnologyChange],
                 [NonTechnologyChangeReason], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@RecipeComponentId, @VersionNumber, @Status, @Instructions, @YieldQuantity, @YieldUnit,
                 @RawWeightGrams, @CookedWeightGrams, @CaloriesPer100g, @ProteinPer100g,
                 @CarbohydratesPer100g, @FatPer100g, @FiberPer100g, @ShelfLifeHours,
                 @UseEarliestIngredientExpiry, @NutritionSource, @NutritionOverrideReason,
                 @AllergensApproved, @AllergenOverrideReason, @AllergensApprovedAt,
                 @AllergensApprovedBy, @ChangeSummary, @IsTechnologyChange,
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
                    ([OwnerType], [MealId], [MealVariantId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                     [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                     [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    [OwnerType], NULL, NULL, @versionId, [StockItemId], [WarehouseCategoryId],
                    [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                    @createdAt, @createdBy, 0
                FROM [PackagingRequirements]
                WHERE [RecipeComponentVersionId] = @sourceVersionId
                  AND [IsDeleted] = 0;

                INSERT INTO [RecipeComponentInstructionSections]
                    ([RecipeComponentVersionId], [Title], [SortOrder], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    @versionId, [Title], [SortOrder], @createdAt, @createdBy, 0
                FROM [RecipeComponentInstructionSections]
                WHERE [RecipeComponentVersionId] = @sourceVersionId
                  AND [IsDeleted] = 0;

                INSERT INTO [RecipeComponentInstructionSteps]
                    ([RecipeComponentInstructionSectionId], [StepText], [SortOrder], [RequiresControl],
                     [ControlType], [ExpectedValue], [ExpectedUnit], [IsCritical], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    targetSections.[Id],
                    sourceSteps.[StepText],
                    sourceSteps.[SortOrder],
                    sourceSteps.[RequiresControl],
                    sourceSteps.[ControlType],
                    sourceSteps.[ExpectedValue],
                    sourceSteps.[ExpectedUnit],
                    sourceSteps.[IsCritical],
                    @createdAt,
                    @createdBy,
                    0
                FROM [RecipeComponentInstructionSections] sourceSections
                INNER JOIN [RecipeComponentInstructionSections] targetSections
                    ON targetSections.[RecipeComponentVersionId] = @versionId
                   AND targetSections.[SortOrder] = sourceSections.[SortOrder]
                   AND ISNULL(targetSections.[Title], N'') = ISNULL(sourceSections.[Title], N'')
                INNER JOIN [RecipeComponentInstructionSteps] sourceSteps
                    ON sourceSteps.[RecipeComponentInstructionSectionId] = sourceSections.[Id]
                WHERE sourceSections.[RecipeComponentVersionId] = @sourceVersionId
                  AND sourceSections.[IsDeleted] = 0
                  AND targetSections.[IsDeleted] = 0
                  AND sourceSteps.[IsDeleted] = 0;
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
                [NutritionSource] = @NutritionSource,
                [NutritionOverrideReason] = @NutritionOverrideReason,
                [AllergensApproved] = @AllergensApproved,
                [AllergenOverrideReason] = @AllergenOverrideReason,
                [AllergensApprovedAt] = @AllergensApprovedAt,
                [AllergensApprovedBy] = @AllergensApprovedBy,
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
                SET [MealVariantId] = @MealVariantId,
                    [StockItemId] = @StockItemId,
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
                ([OwnerType], [MealId], [MealVariantId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@OwnerType, @MealId, @MealVariantId, @RecipeComponentVersionId, @StockItemId, @WarehouseCategoryId,
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

    public async Task<int> SaveInstructionSectionAsync(RecipeComponentInstructionSection section)
    {
        using var db = this.factory.CreateConnection();

        if (section.Id > 0)
        {
            await db.ExecuteAsync(
                """
                UPDATE [RecipeComponentInstructionSections]
                SET [Title] = @Title,
                    [SortOrder] = @SortOrder,
                    [UpdatedAt] = @UpdatedAt,
                    [UpdatedBy] = @UpdatedBy
                WHERE [Id] = @Id
                  AND [RecipeComponentVersionId] IN (
                      SELECT [Id]
                      FROM [RecipeComponentVersions]
                      WHERE [Status] = N'Draft'
                        AND [IsDeleted] = 0
                  )
                  AND [IsDeleted] = 0;
                """,
                section);

            return section.Id;
        }

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [RecipeComponentInstructionSections]
                ([RecipeComponentVersionId], [Title], [SortOrder], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@RecipeComponentVersionId, @Title, @SortOrder, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            section);
    }

    public async Task<int> SaveInstructionStepAsync(RecipeComponentInstructionStep step)
    {
        using var db = this.factory.CreateConnection();

        if (step.Id > 0)
        {
            await db.ExecuteAsync(
                """
                UPDATE [RecipeComponentInstructionSteps]
                SET [StepText] = @StepText,
                    [SortOrder] = @SortOrder,
                    [RequiresControl] = @RequiresControl,
                    [ControlType] = @ControlType,
                    [ExpectedValue] = @ExpectedValue,
                    [ExpectedUnit] = @ExpectedUnit,
                    [IsCritical] = @IsCritical,
                    [UpdatedAt] = @UpdatedAt,
                    [UpdatedBy] = @UpdatedBy
                WHERE [Id] = @Id
                  AND [RecipeComponentInstructionSectionId] IN (
                      SELECT s.[Id]
                      FROM [RecipeComponentInstructionSections] s
                      INNER JOIN [RecipeComponentVersions] v ON v.[Id] = s.[RecipeComponentVersionId]
                      WHERE v.[Status] = N'Draft'
                        AND v.[IsDeleted] = 0
                        AND s.[IsDeleted] = 0
                  )
                  AND [IsDeleted] = 0;
                """,
                step);

            return step.Id;
        }

        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [RecipeComponentInstructionSteps]
                ([RecipeComponentInstructionSectionId], [StepText], [SortOrder], [RequiresControl],
                 [ControlType], [ExpectedValue], [ExpectedUnit], [IsCritical], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@RecipeComponentInstructionSectionId, @StepText, @SortOrder, @RequiresControl,
                 @ControlType, @ExpectedValue, @ExpectedUnit, @IsCritical, @CreatedAt, @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            step);
    }

    public async Task DeleteInstructionSectionAsync(int sectionId, string? deletedBy)
    {
        using var db = this.factory.CreateConnection();
        await db.ExecuteAsync(
            """
            UPDATE [RecipeComponentInstructionSteps]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [RecipeComponentInstructionSectionId] = @sectionId
              AND [IsDeleted] = 0;

            UPDATE [RecipeComponentInstructionSections]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [Id] = @sectionId
              AND [RecipeComponentVersionId] IN (
                  SELECT [Id]
                  FROM [RecipeComponentVersions]
                  WHERE [Status] = N'Draft'
                    AND [IsDeleted] = 0
              )
              AND [IsDeleted] = 0;
            """,
            new { sectionId, deletedAt = DateTimeOffset.UtcNow, deletedBy });
    }

    public async Task DeleteInstructionStepAsync(int stepId, string? deletedBy)
    {
        using var db = this.factory.CreateConnection();
        await db.ExecuteAsync(
            """
            UPDATE [RecipeComponentInstructionSteps]
            SET [IsDeleted] = 1,
                [DeletedAt] = @deletedAt,
                [DeletedBy] = @deletedBy
            WHERE [Id] = @stepId
              AND [RecipeComponentInstructionSectionId] IN (
                  SELECT s.[Id]
                  FROM [RecipeComponentInstructionSections] s
                  INNER JOIN [RecipeComponentVersions] v ON v.[Id] = s.[RecipeComponentVersionId]
                  WHERE v.[Status] = N'Draft'
                    AND v.[IsDeleted] = 0
                    AND s.[IsDeleted] = 0
              )
              AND [IsDeleted] = 0;
            """,
            new { stepId, deletedAt = DateTimeOffset.UtcNow, deletedBy });
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

    private static (string WhereSql, DynamicParameters Parameters) BuildComponentSearchWhere(
        RecipeComponentSearchQuery query)
    {
        var clauses = new List<string> { "1 = 1" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            clauses.Add("(cr.[Name] LIKE @Like OR cr.[Description] LIKE @Like)");
            parameters.Add("Like", $"%{query.Query.Trim()}%");
        }

        if (query.CategoryId.HasValue)
        {
            clauses.Add("cr.[CategoryId] = @CategoryId");
            parameters.Add("CategoryId", query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.VersionStatus))
        {
            clauses.Add("cr.[LatestVersionStatus] = @VersionStatus");
            parameters.Add("VersionStatus", query.VersionStatus.Trim());
        }

        if (query.AllergenId.HasValue)
        {
            clauses.Add(
                """
                EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [IngredientAllergens] ia ON ia.[IngredientId] = rci.[IngredientId]
                    WHERE rci.[RecipeComponentVersionId] = cr.[LatestVersionId]
                      AND rci.[IsDeleted] = 0
                      AND ia.[AllergenId] = @AllergenId
                )
                """);
            parameters.Add("AllergenId", query.AllergenId.Value);
        }

        if (query.MissingPublicationData)
        {
            clauses.Add("cr.[HasPublicationGaps] = 1");
        }

        return (string.Join(" AND ", clauses), parameters);
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
                rcv.[NutritionSource],
                rcv.[NutritionOverrideReason],
                rcv.[AllergensApproved],
                rcv.[AllergenOverrideReason],
                rcv.[AllergensApprovedAt],
                rcv.[AllergensApprovedBy],
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
