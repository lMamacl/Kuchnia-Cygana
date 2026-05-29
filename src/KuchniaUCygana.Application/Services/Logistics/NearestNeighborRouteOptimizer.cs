using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

public sealed class NearestNeighborRouteOptimizer : IRouteOptimizer
{
    public Task<IReadOnlyList<int>> OptimizeSequenceAsync(
        IReadOnlyList<RouteOptimizationPoint> stops,
        RouteOptimizationPoint? origin = null)
    {
        if (stops.Count <= 1)
        {
            return Task.FromResult<IReadOnlyList<int>>(stops.Select(s => s.Id).ToList());
        }

        var remaining = stops.ToList();
        var ordered = new List<int>(stops.Count);
        var currentLatitude = origin?.Latitude ?? remaining.Average(s => s.Latitude);
        var currentLongitude = origin?.Longitude ?? remaining.Average(s => s.Longitude);

        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderBy(s => CalculateDistanceKm(currentLatitude, currentLongitude, s.Latitude, s.Longitude))
                .ThenBy(s => s.Id)
                .First();

            ordered.Add(next.Id);
            remaining.Remove(next);
            currentLatitude = next.Latitude;
            currentLongitude = next.Longitude;
        }

        return Task.FromResult<IReadOnlyList<int>>(ordered);
    }

    public static double CalculateRouteDistanceKm(IReadOnlyList<RouteOptimizationPoint> orderedStops)
    {
        if (orderedStops.Count <= 1)
        {
            return 0;
        }

        var total = 0d;
        for (var i = 1; i < orderedStops.Count; i++)
        {
            var previous = orderedStops[i - 1];
            var current = orderedStops[i];
            total += CalculateDistanceKm(
                previous.Latitude,
                previous.Longitude,
                current.Latitude,
                current.Longitude);
        }

        return total;
    }

    private static double CalculateDistanceKm(
        double latitudeA,
        double longitudeA,
        double latitudeB,
        double longitudeB)
    {
        const double earthRadiusKm = 6371d;

        var latitudeDistance = ToRadians(latitudeB - latitudeA);
        var longitudeDistance = ToRadians(longitudeB - longitudeA);

        var a = Math.Sin(latitudeDistance / 2) * Math.Sin(latitudeDistance / 2)
            + Math.Cos(ToRadians(latitudeA)) * Math.Cos(ToRadians(latitudeB))
            * Math.Sin(longitudeDistance / 2) * Math.Sin(longitudeDistance / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
