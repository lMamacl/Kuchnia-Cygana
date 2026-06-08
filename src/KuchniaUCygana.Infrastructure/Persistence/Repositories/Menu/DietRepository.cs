using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietRepository : BaseRepository<Diet>, IDietRepository
{
    public DietRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IEnumerable<Diet>> GetActiveWithVariantsAsync()
    {
        using var db = this.Factory.CreateConnection();
        const string dietSql = "SELECT * FROM [Diets] WHERE [IsActive] = 1 AND [IsDeleted] = 0;";
        return await db.QueryAsync<Diet>(dietSql);
    }

    public async Task<IReadOnlyList<ActiveDietVariantRow>> GetActiveDietVariantRowsAsync()
    {
        using var db = this.Factory.CreateConnection();
        var rows = await db.QueryAsync<ActiveDietVariantRow>(
            """
            SELECT
                d.[Id] AS [DietId],
                d.[Name] AS [DietName],
                d.[Description],
                d.[MarketingDescription],
                d.[Status],
                d.[IsActive],
                d.[ThumbnailUrl],
                dv.[Id] AS [DietVariantId],
                dv.[Name] AS [VariantName],
                dv.[TargetCalories],
                dv.[PriceMultiplier],
                dv.[IsDefault]
            FROM [Diets] d
            LEFT JOIN [DietVariants] dv ON dv.[DietId] = d.[Id]
                AND dv.[IsDeleted] = 0
            WHERE d.[IsActive] = 1
              AND d.[IsDeleted] = 0
            ORDER BY d.[Name], dv.[TargetCalories], dv.[Name];
            """);

        return rows.ToList();
    }
}


