using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryIssueRepository : BaseRepository<DeliveryIssue>, IDeliveryIssueRepository
{
    public DeliveryIssueRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IReadOnlyList<DeliveryIssue>> GetByRouteStopIdsAsync(IEnumerable<int> routeStopIds)
    {
        var ids = routeStopIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<DeliveryIssue>();
        }

        using var db = this.Factory.CreateConnection();
        var issues = await db.QueryAsync<DeliveryIssue>(
            """
            SELECT *
            FROM [DeliveryIssues]
            WHERE [RouteStopId] IN @Ids
              AND [IsDeleted] = 0
            ORDER BY [ReportedAt] DESC, [Id] DESC;
            """,
            new { Ids = ids });

        return issues.ToList();
    }
}
