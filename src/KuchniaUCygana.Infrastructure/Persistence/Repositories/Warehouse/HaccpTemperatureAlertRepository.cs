using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class HaccpTemperatureAlertRepository
    : BaseRepository<HaccpTemperatureAlert, long>, IHaccpTemperatureAlertRepository
{
    public HaccpTemperatureAlertRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<HaccpTemperatureAlert?> GetOpenByLocationAsync(int haccpLocationId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<HaccpTemperatureAlert>(
            """
            SELECT TOP 1 *
            FROM [HaccpTemperatureAlerts]
            WHERE [HaccpLocationId] = @haccpLocationId
              AND [Status] = @status
            ORDER BY [OpenedAt] DESC, [Id] DESC;
            """,
            new { haccpLocationId, status = HaccpTemperatureAlertStatus.Open });
    }
}


