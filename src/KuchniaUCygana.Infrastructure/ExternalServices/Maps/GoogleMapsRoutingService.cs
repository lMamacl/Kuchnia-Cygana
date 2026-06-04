using KuchniaUCygana.Domain.Interfaces.Logistics;

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
        // Placeholder pod przyszla integracje z Google Directions API / OSRM.
        return Task.FromResult<IReadOnlyList<int>>(stops.Select(s => s.Id).ToList());
    }
}
