using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class CookingSessionStepCheckRepository
    : BaseRepository<CookingSessionStepCheck>, ICookingSessionStepCheckRepository
{
    public CookingSessionStepCheckRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<CookingSessionStepCheck?> GetBySessionAndStepAsync(
        int cookingSessionId,
        int instructionStepId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<CookingSessionStepCheck>(
            """
            SELECT TOP (1) *
            FROM [CookingSessionStepChecks]
            WHERE [CookingSessionId] = @cookingSessionId
              AND [RecipeComponentInstructionStepId] = @instructionStepId
            ORDER BY [Id] DESC;
            """,
            new { cookingSessionId, instructionStepId });
    }

    public async Task<IReadOnlyList<CookingSessionStepCheck>> GetBySessionAsync(int cookingSessionId)
    {
        using var db = Factory.CreateConnection();
        var checks = await db.QueryAsync<CookingSessionStepCheck>(
            """
            SELECT *
            FROM [CookingSessionStepChecks]
            WHERE [CookingSessionId] = @cookingSessionId
            ORDER BY [RecipeComponentInstructionStepId], [Id];
            """,
            new { cookingSessionId });

        return checks.ToList();
    }
}
