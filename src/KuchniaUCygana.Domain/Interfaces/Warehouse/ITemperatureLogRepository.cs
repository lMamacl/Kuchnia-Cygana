using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface ITemperatureLogRepository : IRepository<TemperatureLog, long>
{
    /// <summary>
    /// Logi temperatur z podanego zakresu dat — dla raportów HACCP.
    /// </summary>
    Task<IEnumerable<TemperatureLog>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Logi temperatur z konkretnej lokalizacji (np. "Chłodnia A").
    /// </summary>
    Task<IEnumerable<TemperatureLog>> GetByLocationAsync(string location);

    Task<IEnumerable<TemperatureLog>> GetByLocationIdDateRangeAsync(
        int haccpLocationId,
        DateTimeOffset from,
        DateTimeOffset to);
}
