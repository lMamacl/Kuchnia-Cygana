using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class TemperatureLogRepository : BaseRepository<TemperatureLog, long>, ITemperatureLogRepository
{
    public TemperatureLogRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<TemperatureLog>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<TemperatureLog>(
            t => t.RecordedAt >= from &&
                 t.RecordedAt <= to &&
                 !t.IsDeleted);
    }

    public async Task<IEnumerable<TemperatureLog>> GetByLocationAsync(string location)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<TemperatureLog>(
            t => t.DeviceNameOrLocation == location && !t.IsDeleted);
    }
}
