using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingManifestIssueRepository : BaseRepository<PackingManifestIssue>, IPackingManifestIssueRepository
{
    public PackingManifestIssueRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<PackingManifestIssue>> GetByRouteAsync(DateOnly date, int routeId)
    {
        using var db = Factory.CreateConnection();
        var issues = await db.QueryAsync<PackingManifestIssue>(
            """
            SELECT *
            FROM PackingManifestIssues
            WHERE PackingDate = @date
              AND RouteId = @routeId
            ORDER BY IsBlocking DESC, Status, ReportedAt DESC, Id DESC;
            """,
            new
            {
                date = date.ToDateTime(TimeOnly.MinValue),
                routeId,
            });

        return issues.ToList();
    }
}


