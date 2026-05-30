using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IHaccpTemperatureAlertRepository : IRepository<HaccpTemperatureAlert, long>
{
    Task<HaccpTemperatureAlert?> GetOpenByLocationAsync(int haccpLocationId);
}
