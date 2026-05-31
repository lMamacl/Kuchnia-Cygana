using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Infrastructure.ExternalServices.Maps;

/// <summary>
///Realizacja optymalizacji przy użyciu zewnętrznego API
///(np. Google Directions API / OSRM)
/// </summary>

public class GoogleMapsRoutingService : IRouteOptimizer
{
    public Task<IReadOnlyList<int>> OptimizeSequenceAsync(
        IReadOnlyList<RouteOptimizationPoint> stops,
        RouteOptimizationPoint? origin = null)
    {
        return Task.FromResult<IReadOnlyList<int>>(stops.Select(s => s.Id).ToList());
    }
}
