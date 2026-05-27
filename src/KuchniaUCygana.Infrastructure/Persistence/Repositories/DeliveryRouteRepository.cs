using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

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
        
        // Pobranie trasy
        var routeSql = @"
            SELECT * FROM [DeliveryRoutes] 
            WHERE [Id] = @RouteId AND [IsDeleted] = 0";
        var route = await db.QueryFirstOrDefaultAsync<DeliveryRoute>(routeSql, new { RouteId = routeId });
        
        if (route == null) return null;
        
        // Pobranie przystanków
        var stopsSql = @"
            SELECT * FROM [DeliveryRouteStops] 
            WHERE [RouteId] = @RouteId AND [IsDeleted] = 0 
            ORDER BY [SequenceNumber]";
        var stops = await db.QueryAsync<DeliveryRouteStop>(stopsSql, new { RouteId = routeId });
        
        route.Stops = stops.ToList();
        return route;
    }



    public async Task<List<DeliveryRoute>> GetRoutesWithStopsAsync(DateTimeOffset date)
{
    using var db = Factory.CreateConnection();
    var sqlRoutes = "SELECT * FROM [DeliveryRoutes] WHERE CAST([RouteDate] AS DATE) = CAST(@Date AS DATE) AND [IsDeleted] = 0";
    var routes = (await db.QueryAsync<DeliveryRoute>(sqlRoutes, new { Date = date.Date })).ToList();
    
    foreach (var route in routes)
    {
        var sqlStops = "SELECT * FROM [DeliveryRouteStops] WHERE [RouteId] = @RouteId AND [IsDeleted] = 0 ORDER BY [SequenceNumber]";
        var stops = await db.QueryAsync<DeliveryRouteStop>(sqlStops, new { RouteId = route.Id });
        route.Stops = stops.ToList();
    }
    return routes;
}
}