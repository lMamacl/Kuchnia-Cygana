using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Infrastructure.Mocks;

/// <summary>
/// Jawny tryb test/dev dla danych logistycznych.
/// Runtime uzywa M4DeliveryManifestProvider.
/// </summary>
public sealed class MockDeliveryManifestProvider : IDeliveryManifestProvider
{
    public Task<IEnumerable<RouteEntry>> GetRoutesForDateAsync(DateOnly date)
    {
        var orderCount = MockDeliveryPlanData.GetOrderCount(date);
        var routeCount = orderCount <= 16 ? 2 : 3;
        var routeSize = (int)Math.Ceiling(orderCount / (double)routeCount);

        var routeNames = new[]
        {
            "Mokotow-Poludnie",
            "Ursynow-Wilanow",
            "Srodmiescie-Wola",
        };

        var vehicleRegistrations = new[]
        {
            "WA 12345",
            "WA 67890",
            "WA 11223",
        };

        var routes = new List<RouteEntry>();
        for (var routeIndex = 0; routeIndex < routeCount; routeIndex++)
        {
            var startIndex = routeIndex * routeSize;
            var stopCount = Math.Min(routeSize, orderCount - startIndex);
            if (stopCount <= 0)
            {
                continue;
            }

            routes.Add(new RouteEntry
            {
                RouteId = routeIndex + 1,
                RouteName = routeNames[routeIndex],
                VehicleId = routeIndex + 1,
                VehicleRegistration = vehicleRegistrations[routeIndex],
                Stops = GenerateStops(routeIndex + 1, startIndex, stopCount, date),
            });
        }

        return Task.FromResult<IEnumerable<RouteEntry>>(routes);
    }

    public Task<RouteEntry?> GetRouteByIdAsync(int routeId)
    {
        var route = GetRoutesForDateAsync(DateOnly.FromDateTime(DateTime.Today))
            .Result
            .FirstOrDefault(r => r.RouteId == routeId);

        return Task.FromResult(route);
    }

    private static List<RouteStopEntry> GenerateStops(int routeId, int startIndex, int count, DateOnly date)
    {
        var stops = new List<RouteStopEntry>();

        for (var i = 1; i <= count; i++)
        {
            var orderIndex = startIndex + i - 1;
            var windowStart = new TimeOnly(8 + orderIndex / 4, (orderIndex % 4) * 15);
            var windowEnd = windowStart.AddMinutes(30);

            stops.Add(new RouteStopEntry
            {
                StopId = routeId * 1000 + i,
                SequenceNumber = i,
                DeliveryWindowFrom = windowStart.ToString("HH:mm"),
                DeliveryWindowTo = windowEnd.ToString("HH:mm"),
                DeliveryCalendarId = MockDeliveryPlanData.CreateDeliveryCalendarId(date, orderIndex),
            });
        }

        return stops;
    }
}
