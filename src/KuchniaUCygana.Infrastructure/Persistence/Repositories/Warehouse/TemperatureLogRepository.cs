using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class TemperatureLogRepository : BaseRepository<TemperatureLog, long>, ITemperatureLogRepository
{
    public TemperatureLogRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<TemperatureLog>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<TemperatureLog>(
            """
            SELECT *
            FROM [TemperatureLogs]
            WHERE [RecordedAt] >= @from
              AND [RecordedAt] <= @to
              AND [IsDeleted] = 0
            ORDER BY [RecordedAt], [Id];
            """,
            new { from, to });
    }

    public async Task<IEnumerable<TemperatureLog>> GetByLocationAsync(string location)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<TemperatureLog>(
            """
            SELECT *
            FROM [TemperatureLogs]
            WHERE [DeviceNameOrLocation] = @location
              AND [IsDeleted] = 0
            ORDER BY [RecordedAt], [Id];
            """,
            new { location });
    }
}
