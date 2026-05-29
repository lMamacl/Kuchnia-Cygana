using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na trasach
/// </summary>

public interface IDeliveryRouteRepository : IRepository<DeliveryRoute>
{
    Task<DeliveryRoute?> GetRouteWithStopsAsync(int routeId);
    Task<List<DeliveryRoute>> GetRoutesWithStopsAsync(DateTimeOffset date);
}

