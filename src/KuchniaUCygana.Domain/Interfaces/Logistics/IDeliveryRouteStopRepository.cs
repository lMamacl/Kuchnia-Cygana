using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na trasach
/// </summary>

public interface IDeliveryRouteStopRepository : IRepository<DeliveryRouteStop>
{
    //Task<IEnumerable<DeliveryRouteStop>> GetStopsForRouteAsync(int routeId);
    //Task<IEnumerable<DeliveryRouteStop>> GetStopsForDateAsync(DateTime date);
}