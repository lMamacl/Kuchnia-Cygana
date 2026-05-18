using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Domain.Entities.Logistics;

namespace KuchniaUCygana.Infrastructure.ExternalServices.Maps;

/// <summary>
///Realizacja optymalizacji przy użyciu zewnętrznego API
///(np. Google Directions API / OSRM)
/// </summary>

public class GoogleMapsRoutingService : IRouteOptimizer
{
    // Implementacja w kroku 5; odkomentuję jak złączę 1 z 4 bo używa Address z 1
    //async Task<List<int>> IRouteOptimizer.OptimizeSequenceAsync(List<Address> stops)
    //{
    //    throw new NotImplementedException();
    //}
}
