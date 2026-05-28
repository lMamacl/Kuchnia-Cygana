using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Infrastructure.Mocks;

/// <summary>
/// Mock dostawcy danych logistycznych (Moduł 4 niedostępny).
/// Zwraca 3 trasy z realistycznymi danymi.
/// Zastąpione adapterem M4 w fazie integracji.
/// </summary>
public sealed class MockDeliveryManifestProvider : IDeliveryManifestProvider
{
    public Task<IEnumerable<RouteEntry>> GetRoutesForDateAsync(DateOnly date)
    {
        var routes = new List<RouteEntry>
        {
            new()
            {
                RouteId = 1,
                RouteName = "Mokotów-Południe",
                VehicleId = 1,
                VehicleRegistration = "WA 12345",
                Stops = GenerateStops(1, 8, date),
            },
            new()
            {
                RouteId = 2,
                RouteName = "Ursynów-Wilanów",
                VehicleId = 2,
                VehicleRegistration = "WA 67890",
                Stops = GenerateStops(2, 10, date),
            },
            new()
            {
                RouteId = 3,
                RouteName = "Śródmieście-Wola",
                VehicleId = 3,
                VehicleRegistration = "WA 11223",
                Stops = GenerateStops(3, 6, date),
            },
        };

        return Task.FromResult<IEnumerable<RouteEntry>>(routes);
    }

    public Task<RouteEntry?> GetRouteByIdAsync(int routeId)
    {
        var routes = GetRoutesForDateAsync(DateOnly.FromDateTime(DateTime.Today)).Result;
        var route = ((List<RouteEntry>)routes).Find(r => r.RouteId == routeId);
        return Task.FromResult(route);
    }

    private static List<RouteStopEntry> GenerateStops(int routeId, int count, DateOnly date)
    {
        var stops = new List<RouteStopEntry>();
        var baseHour = 8; // Pierwszy stop o 8:00

        for (var i = 1; i <= count; i++)
        {
            var windowStart = new TimeOnly(baseHour + (i - 1) / 2, (i % 2 == 0) ? 30 : 0);
            var windowEnd = windowStart.AddMinutes(30);

            stops.Add(new RouteStopEntry
            {
                StopId = routeId * 100 + i,
                SequenceNumber = i,
                DeliveryWindowFrom = windowStart.ToString("HH:mm"),
                DeliveryWindowTo = windowEnd.ToString("HH:mm"),
                DeliveryCalendarId = date.DayNumber * 100 + (routeId - 1) * count + i,
            });
        }

        return stops;
    }
}
