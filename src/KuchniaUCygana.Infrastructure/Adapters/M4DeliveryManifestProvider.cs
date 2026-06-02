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
            WHERE r.[RouteDate] >= @From AND r.[RouteDate] < @To AND r.[IsDeleted] = 0;";

        var from = date.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);
        var routes = (await db.QueryAsync<RouteEntry>(routesSql, new { From = from, To = to })).ToList();

        if (routes.Count == 0)
        {
            return routes;
        }

        const string stopsSql = @"
            SELECT 
                s.[RouteId] AS RouteId,
                s.[Id] AS StopId,
                s.[SequenceNumber] AS SequenceNumber,
                s.[DeliveryCalendarId] AS DeliveryCalendarId,
                s.[PlannedArrivalTime] AS PlannedArrivalTime,
                w.[StartTime] AS DeliveryWindowFrom,
                w.[EndTime] AS DeliveryWindowTo
            FROM [DeliveryRouteStops] s
            LEFT JOIN [DeliveryCalendar] c ON s.[DeliveryCalendarId] = c.[Id]
            LEFT JOIN [DeliveryWindows] w ON c.[DeliveryWindowId] = w.[Id]
            WHERE s.[RouteId] IN @RouteIds AND s.[IsDeleted] = 0
            ORDER BY s.[RouteId], s.[SequenceNumber];";

        var rawStops = (await db.QueryAsync<RouteStopRecord>(stopsSql, new { RouteIds = routes.Select(route => route.RouteId).ToArray() })).ToList();
        var stopsByRoute = rawStops
            .GroupBy(row => row.RouteId)
            .ToDictionary(group => group.Key, group => group.Select(MapStop).ToList());

        foreach (var route in routes)
        {
            route.Stops = stopsByRoute.TryGetValue(route.RouteId, out var stops)
                ? stops
                : new List<RouteStopEntry>();
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

        var rawStops = (await db.QueryAsync<RouteStopRecord>(stopsSql, new { RouteId = routeId })).ToList();
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
                StopId = row.StopId,
                SequenceNumber = row.SequenceNumber,
                DeliveryCalendarId = row.DeliveryCalendarId,
                DeliveryWindowFrom = from,
                DeliveryWindowTo = to
            });
        }

        return stops;
    }

    private static RouteStopEntry MapStop(RouteStopRecord row)
    {
        string from = row.DeliveryWindowFrom ?? string.Empty;
        string to = row.DeliveryWindowTo ?? string.Empty;
        DateTimeOffset? planned = row.PlannedArrivalTime;

        if (string.IsNullOrEmpty(from) && planned.HasValue)
        {
            from = planned.Value.AddMinutes(-30).ToString("HH:mm");
        }

        if (string.IsNullOrEmpty(to) && planned.HasValue)
        {
            to = planned.Value.AddMinutes(30).ToString("HH:mm");
        }

        return new RouteStopEntry
        {
            StopId = row.StopId,
            SequenceNumber = row.SequenceNumber,
            DeliveryCalendarId = row.DeliveryCalendarId,
            DeliveryWindowFrom = from,
            DeliveryWindowTo = to
        };
    }

    private sealed class RouteStopRecord
    {
        public int RouteId { get; set; }

        public int StopId { get; set; }

        public int SequenceNumber { get; set; }

        public int DeliveryCalendarId { get; set; }

        public DateTimeOffset? PlannedArrivalTime { get; set; }

        public string? DeliveryWindowFrom { get; set; }

        public string? DeliveryWindowTo { get; set; }
    }
}
