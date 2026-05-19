using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium tras dostaw
/// </summary>
public sealed class DeliveryRouteRepository : BaseRepository<DeliveryRoute>, IDeliveryRouteRepository
{
    public DeliveryRouteRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<DeliveryRoute?> GetRouteWithStopsAsync(int routeId)
    {
        using var db = Factory.CreateConnection();
        var route = await db.SingleByIdAsync<DeliveryRoute>(routeId);
        if (route == null) return null;
        var stops = await db.SelectAsync<DeliveryRouteStop>(s => s.RouteId == routeId && s.IsDeleted == false);
        route.Stops = stops.OrderBy(s => s.SequenceNumber).ToList();
        return route;
    }



    public async Task<List<DeliveryRoute>> GetRoutesWithStopsAsync(DateTimeOffset date)
{
    using var db = Factory.CreateConnection();
    var routes = await db.SelectAsync<DeliveryRoute>(r => r.RouteDate.Date == date.Date && r.IsDeleted == false);
    foreach (var route in routes)
    {
        var stops = await db.SelectAsync<DeliveryRouteStop>(s => s.RouteId == route.Id && s.IsDeleted == false);
        route.Stops = stops.OrderBy(s => s.SequenceNumber).ToList();
    }
    return routes;
}
}