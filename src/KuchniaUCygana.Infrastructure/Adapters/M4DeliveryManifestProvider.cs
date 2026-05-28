using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Adapters;

/// <summary>
/// Adapter dostarczający dane logistyczne z modułu M4 przy użyciu Dappera.
/// </summary>
public sealed class M4DeliveryManifestProvider : IDeliveryManifestProvider
{
    private readonly IDbConnectionFactory _connectionFactory;

    public M4DeliveryManifestProvider(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<RouteEntry>> GetRoutesForDateAsync(DateOnly date)
    {
        using var db = _connectionFactory.CreateConnection();
        
        // 1. Pobierz trasy na dany dzień
        const string routesSql = @"
            SELECT 
                r.[Id] AS RouteId,
                r.[Name] AS RouteName,
                r.[VehicleId] AS VehicleId,
                v.[RegistrationNumber] AS VehicleRegistration
            FROM [DeliveryRoutes] r
            LEFT JOIN [Vehicles] v ON r.[VehicleId] = v.[Id]
            WHERE CAST(r.[RouteDate] AS DATE) = CAST(@Date AS DATE) AND r.[IsDeleted] = 0;";

        var dateParam = date.ToDateTime(TimeOnly.MinValue);
        var routes = (await db.QueryAsync<RouteEntry>(routesSql, new { Date = dateParam })).ToList();

        // 2. Pobierz przystanki dla każdej trasy
        foreach (var route in routes)
        {
            route.Stops = await GetStopsForRouteAsync(db, route.RouteId);
        }

        return routes;
    }

    public async Task<RouteEntry?> GetRouteByIdAsync(int routeId)
    {
        using var db = _connectionFactory.CreateConnection();

        const string routeSql = @"
            SELECT 
                r.[Id] AS RouteId,
                r.[Name] AS RouteName,
                r.[VehicleId] AS VehicleId,
                v.[RegistrationNumber] AS VehicleRegistration
            FROM [DeliveryRoutes] r
            LEFT JOIN [Vehicles] v ON r.[VehicleId] = v.[Id]
            WHERE r.[Id] = @RouteId AND r.[IsDeleted] = 0;";

        var route = await db.QueryFirstOrDefaultAsync<RouteEntry>(routeSql, new { RouteId = routeId });
        if (route == null) return null;

        route.Stops = await GetStopsForRouteAsync(db, route.RouteId);
        return route;
    }

    private async Task<List<RouteStopEntry>> GetStopsForRouteAsync(IDbConnection db, int routeId)
    {
        const string stopsSql = @"
            SELECT 
                s.[Id] AS StopId,
                s.[SequenceNumber] AS SequenceNumber,
                s.[DeliveryCalendarId] AS DeliveryCalendarId,
                s.[PlannedArrivalTime] AS PlannedArrivalTime,
                w.[StartTime] AS DeliveryWindowFrom,
                w.[EndTime] AS DeliveryWindowTo
            FROM [DeliveryRouteStops] s
            LEFT JOIN [DeliveryCalendar] c ON s.[DeliveryCalendarId] = c.[Id]
            LEFT JOIN [DeliveryWindows] w ON c.[DeliveryWindowId] = w.[Id]
            WHERE s.[RouteId] = @RouteId AND s.[IsDeleted] = 0
            ORDER BY s.[SequenceNumber];";

        var rawStops = (await db.QueryAsync<dynamic>(stopsSql, new { RouteId = routeId })).ToList();
        var stops = new List<RouteStopEntry>();

        foreach (var row in rawStops)
        {
            string from = row.DeliveryWindowFrom ?? string.Empty;
            string to = row.DeliveryWindowTo ?? string.Empty;
            DateTimeOffset? planned = row.PlannedArrivalTime;

            // Fallback na PlannedArrivalTime, jeśli okno dostawy jest puste
            if (string.IsNullOrEmpty(from) && planned.HasValue)
            {
                from = planned.Value.AddMinutes(-30).ToString("HH:mm");
            }
            if (string.IsNullOrEmpty(to) && planned.HasValue)
            {
                to = planned.Value.AddMinutes(30).ToString("HH:mm");
            }

            stops.Add(new RouteStopEntry
            {
                StopId = (int)row.StopId,
                SequenceNumber = (int)row.SequenceNumber,
                DeliveryCalendarId = (int)row.DeliveryCalendarId,
                DeliveryWindowFrom = from,
                DeliveryWindowTo = to
            });
        }

        return stops;
    }
}
