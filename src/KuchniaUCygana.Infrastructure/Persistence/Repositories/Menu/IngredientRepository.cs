using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class IngredientRepository : BaseRepository<Ingredient>, IIngredientRepository
{
    public IngredientRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IngredientSearchResult> SearchAsync(IngredientSearchQuery query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        using var db = this.Factory.CreateConnection();
        var (whereSql, parameters) = BuildSearchWhere(query);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM [Ingredients] i WHERE {whereSql};",
            parameters);

        var rows = await db.QueryAsync<IngredientListRow>(
            $"""
            SELECT
                i.[Id],
                i.[Name],
                i.[ResourceType],
                i.[FoodCategoryId],
                fc.[Name] AS [FoodCategoryName],
                i.[Unit],
                i.[CostPerUnit],
                i.[Notes],
                COALESCE(i.[StockItemId], stockMapping.[StockItemId]) AS [StockItemId],
                COALESCE(i.[WarehouseCategoryId], stockById.[WarehouseCategoryId], stockMapping.[WarehouseCategoryId]) AS [WarehouseCategoryId],
                COALESCE(wc.[Name], stockMapping.[WarehouseCategoryName]) AS [WarehouseCategoryName],
                CAST(CASE
                    WHEN i.[StockItemId] IS NULL
                     AND i.[WarehouseCategoryId] IS NULL
                     AND NOT EXISTS (
                         SELECT 1
                         FROM [StockItems] si
                         WHERE si.[BaseIngredientId] = i.[Id]
                           AND si.[IsDeleted] = 0
                     )
                    THEN 1
                    ELSE 0
                END AS bit) AS [MissingWarehouseMapping],
                i.[IsActive],
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g],
                allergenList.[AllergenNames]
            FROM [Ingredients] i
            LEFT JOIN [Categories] fc ON fc.[Id] = i.[FoodCategoryId]
            LEFT JOIN [StockItems] stockById ON stockById.[Id] = i.[StockItemId] AND stockById.[IsDeleted] = 0
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(i.[WarehouseCategoryId], stockById.[WarehouseCategoryId])
            OUTER APPLY (
                SELECT TOP 1
                    si.[Id] AS [StockItemId],
                    si.[WarehouseCategoryId],
                    baseWc.[Name] AS [WarehouseCategoryName]
                FROM [StockItems] si
                LEFT JOIN [WarehouseCategories] baseWc ON baseWc.[Id] = si.[WarehouseCategoryId]
                WHERE si.[BaseIngredientId] = i.[Id]
                  AND si.[IsDeleted] = 0
                ORDER BY si.[Id]
            ) stockMapping
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
            OUTER APPLY (
                SELECT STUFF((
                    SELECT N', ' + a.[Name] + CASE WHEN ia.[TraceAmount] = 1 THEN N' (sladowo)' ELSE N'' END
                    FROM [IngredientAllergens] ia
                    INNER JOIN [Allergens] a ON a.[Id] = ia.[AllergenId]
                    WHERE ia.[IngredientId] = i.[Id]
                    ORDER BY a.[Name]
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 2, N'') AS [AllergenNames]
            ) allergenList
            WHERE {whereSql}
            ORDER BY i.[Name], i.[Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new IngredientSearchResult
        {
            Items = rows.ToList(),
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<IngredientAllergenRow>> GetIngredientAllergensAsync(int ingredientId)
    {
        using var db = this.Factory.CreateConnection();
        var rows = await db.QueryAsync<IngredientAllergenRow>(
            """
            SELECT
                ia.[IngredientId],
                ia.[AllergenId],
                a.[Name],
                a.[Code],
                a.[IconUrl],
                ia.[TraceAmount]
            FROM [IngredientAllergens] ia
            INNER JOIN [Allergens] a ON a.[Id] = ia.[AllergenId]
            WHERE ia.[IngredientId] = @ingredientId
            ORDER BY a.[Name];
            """,
            new { ingredientId });

        return rows.ToList();
    }

    public async Task SaveIngredientAllergensAsync(int ingredientId, IReadOnlyList<IngredientAllergen> allergens)
    {
        using var db = this.Factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        await db.ExecuteAsync(
            "DELETE FROM [IngredientAllergens] WHERE [IngredientId] = @ingredientId;",
            new { ingredientId },
            tx);

        var rows = allergens
            .Where(a => a.AllergenId > 0)
            .GroupBy(a => a.AllergenId)
            .Select(group => new
            {
                IngredientId = ingredientId,
                AllergenId = group.Key,
                TraceAmount = group.Any(a => a.TraceAmount),
            })
            .ToArray();

        if (rows.Length > 0)
        {
            await db.ExecuteAsync(
                """
                INSERT INTO [IngredientAllergens] ([IngredientId], [AllergenId], [TraceAmount])
                VALUES (@IngredientId, @AllergenId, @TraceAmount);
                """,
                rows,
                tx);
        }

        tx.Commit();
    }

    public async Task<bool> CanDeleteAsync(int ingredientId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = """
            SELECT
                (SELECT COUNT(1)
                 FROM [Recipes]
                 WHERE [IngredientId] = @IngredientId
                   AND [IsDeleted] = 0)
              + (SELECT COUNT(1)
                 FROM [RecipeComponentIngredients]
                 WHERE [IngredientId] = @IngredientId
                   AND [IsDeleted] = 0);
            """;
        var count = await db.ExecuteScalarAsync<int>(sql, new { IngredientId = ingredientId });
        return count == 0;
    }

    private static (string WhereSql, DynamicParameters Parameters) BuildSearchWhere(IngredientSearchQuery query)
    {
        var clauses = new List<string> { "i.[IsDeleted] = 0" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            clauses.Add("(i.[Name] LIKE @SearchLike OR i.[Description] LIKE @SearchLike OR i.[ProductComposition] LIKE @SearchLike OR i.[Notes] LIKE @SearchLike)");
            parameters.Add("SearchLike", $"%{query.Search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
        {
            clauses.Add("i.[ResourceType] = @ResourceType");
            parameters.Add("ResourceType", query.ResourceType.Trim());
        }

        if (query.FoodCategoryId.HasValue)
        {
            clauses.Add("i.[FoodCategoryId] = @FoodCategoryId");
            parameters.Add("FoodCategoryId", query.FoodCategoryId.Value);
        }

        if (query.WarehouseCategoryId.HasValue)
        {
            clauses.Add(
                """
                (
                    i.[WarehouseCategoryId] = @WarehouseCategoryId
                    OR EXISTS (
                        SELECT 1
                        FROM [StockItems] si
                        WHERE si.[IsDeleted] = 0
                          AND si.[WarehouseCategoryId] = @WarehouseCategoryId
                          AND (si.[Id] = i.[StockItemId] OR si.[BaseIngredientId] = i.[Id])
                    )
                )
                """);
            parameters.Add("WarehouseCategoryId", query.WarehouseCategoryId.Value);
        }

        if (query.AllergenId.HasValue)
        {
            clauses.Add(
                """
                EXISTS (
                    SELECT 1
                    FROM [IngredientAllergens] ia
                    WHERE ia.[IngredientId] = i.[Id]
                      AND ia.[AllergenId] = @AllergenId
                )
                """);
            parameters.Add("AllergenId", query.AllergenId.Value);
        }

        if (query.MissingWarehouseMapping)
        {
            clauses.Add(
                """
                i.[StockItemId] IS NULL
                AND i.[WarehouseCategoryId] IS NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM [StockItems] si
                    WHERE si.[BaseIngredientId] = i.[Id]
                      AND si.[IsDeleted] = 0
                )
                """);
        }

        if (query.IsActive.HasValue)
        {
            clauses.Add("i.[IsActive] = @IsActive");
            parameters.Add("IsActive", query.IsActive.Value);
        }

        return (string.Join(" AND ", clauses), parameters);
    }
}


