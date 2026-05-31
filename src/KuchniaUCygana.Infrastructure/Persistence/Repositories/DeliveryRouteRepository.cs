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
        var from = date.Date;
        var to = from.AddDays(1);
        const string sqlRoutes = """
            SELECT *
            FROM [DeliveryRoutes]
            WHERE [RouteDate] >= @From
              AND [RouteDate] < @To
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """;
        var routes = (await db.QueryAsync<DeliveryRoute>(sqlRoutes, new { From = from, To = to })).ToList();

        if (routes.Count == 0)
        {
            return routes;
        }

        const string sqlStops = """
            SELECT *
            FROM [DeliveryRouteStops]
            WHERE [RouteId] IN @RouteIds
              AND [IsDeleted] = 0
            ORDER BY [RouteId], [SequenceNumber];
            """;
        var stops = (await db.QueryAsync<DeliveryRouteStop>(
            sqlStops,
            new { RouteIds = routes.Select(route => route.Id).ToArray() })).ToList();
        var stopsByRoute = stops
            .GroupBy(stop => stop.RouteId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var route in routes)
        {
            route.Stops = stopsByRoute.TryGetValue(route.Id, out var routeStops)
                ? routeStops
                : new List<DeliveryRouteStop>();
        }

        return routes;
    }
}
