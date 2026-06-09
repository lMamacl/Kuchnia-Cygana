using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class RouteMapViewModel
{
    public DateTimeOffset SelectedDate { get; set; }
    public int? SelectedRouteId { get; set; }
    public IReadOnlyList<DeliveryRouteDto> Routes { get; set; } = [];

    public bool IsSingleRoute => SelectedRouteId.HasValue;
    public int TotalStops => Routes.Sum(route => route.Stops.Count);
    public int GeocodedStops => Routes.Sum(route => route.Stops.Count(stop => stop.Latitude.HasValue && stop.Longitude.HasValue));
    public int MissingCoordinatesStops => TotalStops - GeocodedStops;
    public double TotalDistanceKm => Routes.Sum(route => route.TotalDistanceKm);
}
