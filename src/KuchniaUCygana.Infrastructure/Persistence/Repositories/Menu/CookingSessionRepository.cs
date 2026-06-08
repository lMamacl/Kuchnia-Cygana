using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class CookingSessionRepository : BaseRepository<CookingSession>, ICookingSessionRepository
{
    public CookingSessionRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<CookingSession?> GetLatestForComponentAsync(
        int productionPlanItemId,
        int recipeComponentVersionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<CookingSession>(
            """
            SELECT TOP (1) *
            FROM [CookingSessions]
            WHERE [ProductionPlanItemId] = @productionPlanItemId
              AND [RecipeComponentVersionId] = @recipeComponentVersionId
            ORDER BY [Id] DESC;
            """,
            new { productionPlanItemId, recipeComponentVersionId });
    }
}
