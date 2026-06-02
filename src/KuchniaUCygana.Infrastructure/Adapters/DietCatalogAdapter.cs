using Dapper;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Adapters;

public sealed class DietCatalogAdapter : IDietCatalogProvider
{
    private readonly IDbConnectionFactory connectionFactory;

    public DietCatalogAdapter(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<DietCatalogDto> GetCurrentCatalogAsync(DietCatalogQuery? query = null)
    {
        using var db = this.connectionFactory.CreateConnection();
        var search = NormalizeSearch(query?.SearchTerm);
        var includeInactive = query?.IncludeInactive ?? false;

        var diets = (await db.QueryAsync<DietCatalogItemDto>(
            """
            SELECT
                d.[Id] AS [DietId],
                d.[Name],
                d.[Description],
                d.[MarketingDescription],
                d.[Status],
                d.[IsActive],
                d.[ThumbnailUrl]
            FROM [Diets] d
            WHERE d.[IsDeleted] = 0
              AND (@includeInactive = 1 OR (d.[IsActive] = 1 AND d.[Status] IN (N'Published', N'Active')))
              AND (@search IS NULL
                   OR d.[Name] LIKE @like
                   OR d.[Description] LIKE @like
                   OR d.[MarketingDescription] LIKE @like)
            ORDER BY d.[Name], d.[Id];
            """,
            new
            {
                includeInactive,
                search,
                like = $"%{search}%",
            })).ToList();

        await AttachVariantsAsync(db, diets, includeInactive);

        return new DietCatalogDto
        {
            Diets = diets,
        };
    }

    public async Task<DietCatalogItemDto?> GetDietAsync(int dietId)
    {
        using var db = this.connectionFactory.CreateConnection();
        var diet = await db.QuerySingleOrDefaultAsync<DietCatalogItemDto>(
            """
            SELECT
                d.[Id] AS [DietId],
                d.[Name],
                d.[Description],
                d.[MarketingDescription],
                d.[Status],
                d.[IsActive],
                d.[ThumbnailUrl]
            FROM [Diets] d
            WHERE d.[Id] = @dietId
              AND d.[IsDeleted] = 0;
            """,
            new { dietId });

        if (diet is null)
        {
            return null;
        }

        await AttachVariantsAsync(db, new List<DietCatalogItemDto> { diet }, includeInactive: true);
        return diet;
    }

    public async Task<DietCatalogVariantDto?> GetVariantAsync(int dietVariantId)
    {
        using var db = this.connectionFactory.CreateConnection();

        return await db.QuerySingleOrDefaultAsync<DietCatalogVariantDto>(
            """
            SELECT
                dv.[Id] AS [DietVariantId],
                dv.[DietId],
                dv.[Name],
                dv.[TargetCalories],
                dv.[PriceMultiplier],
                dv.[IsDefault],
                CAST(CASE
                    WHEN dv.[IsDeleted] = 0
                     AND d.[IsDeleted] = 0
                     AND d.[IsActive] = 1
                     AND d.[Status] IN (N'Published', N'Active')
                    THEN 1 ELSE 0 END AS bit) AS [IsAvailable]
            FROM [DietVariants] dv
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
            WHERE dv.[Id] = @dietVariantId;
            """,
            new { dietVariantId });
    }

    private static async Task AttachVariantsAsync(
        System.Data.IDbConnection db,
        IReadOnlyList<DietCatalogItemDto> diets,
        bool includeInactive)
    {
        var dietIds = diets.Select(d => d.DietId).Distinct().ToArray();
        if (dietIds.Length == 0)
        {
            return;
        }

        var variants = (await db.QueryAsync<DietCatalogVariantDto>(
            """
            SELECT
                dv.[Id] AS [DietVariantId],
                dv.[DietId],
                dv.[Name],
                dv.[TargetCalories],
                dv.[PriceMultiplier],
                dv.[IsDefault],
                CAST(CASE
                    WHEN dv.[IsDeleted] = 0
                     AND d.[IsDeleted] = 0
                     AND d.[IsActive] = 1
                     AND d.[Status] IN (N'Published', N'Active')
                    THEN 1 ELSE 0 END AS bit) AS [IsAvailable]
            FROM [DietVariants] dv
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
            WHERE dv.[DietId] IN @dietIds
              AND (@includeInactive = 1 OR dv.[IsDeleted] = 0)
            ORDER BY dv.[DietId], dv.[IsDefault] DESC, dv.[TargetCalories], dv.[Name];
            """,
            new { dietIds, includeInactive })).ToList();

        var variantsByDiet = variants
            .GroupBy(v => v.DietId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DietCatalogVariantDto>)g.ToList());

        foreach (var diet in diets)
        {
            diet.Variants = variantsByDiet.GetValueOrDefault(diet.DietId) ?? Array.Empty<DietCatalogVariantDto>();
        }
    }

    private static string? NormalizeSearch(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
