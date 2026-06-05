using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium tras dostaw
/// </summary>
public sealed class DeliveryRouteRepository : BaseRepository<DeliveryRoute>, IDeliveryRouteRepository
{
    public DeliveryRouteRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
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
        
        // Pobranie przystankĂłw
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
        var routeDate = date.Date;
        const string sqlRoutes = """
            SELECT *
            FROM [DeliveryRoutes]
            WHERE CAST([RouteDate] AS date) = @RouteDate
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """;
        var routes = (await db.QueryAsync<DeliveryRoute>(sqlRoutes, new { RouteDate = routeDate })).ToList();

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

    public async Task InsertManyWithStopsAsync(IReadOnlyCollection<DeliveryRoute> routes)
    {
        if (routes.Count == 0)
        {
            return;
        }

        using var db = Factory.CreateConnection();
        db.Open();
        using var transaction = db.BeginTransaction();

        try
        {
            const string routeSql = """
                INSERT INTO [DeliveryRoutes]
                    ([RouteDate], [Name], [TotalDistanceKm], [Status], [VehicleId], [DriverId],
                     [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@RouteDate, @Name, @TotalDistanceKm, @Status, @VehicleId, @DriverId,
                     @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy);
                """;
            const string stopSql = """
                INSERT INTO [DeliveryRouteStops]
                    ([RouteId], [DeliveryCalendarId], [SequenceNumber], [PlannedArrivalTime],
                     [ActualArrivalTime], [Status], [CreatedAt], [UpdatedAt], [CreatedBy],
                     [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@RouteId, @DeliveryCalendarId, @SequenceNumber, @PlannedArrivalTime,
                     @ActualArrivalTime, @Status, @CreatedAt, @UpdatedAt, @CreatedBy,
                     @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy);
                """;

            var now = DateTimeOffset.UtcNow;
            foreach (var route in routes)
            {
                route.CreatedAt = now;
                route.Id = await db.ExecuteScalarAsync<int>(routeSql, route, transaction);

                foreach (var stop in route.Stops)
                {
                    stop.RouteId = route.Id;
                    stop.CreatedAt = now;
                    stop.Id = await db.ExecuteScalarAsync<int>(stopSql, stop, transaction);
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}


