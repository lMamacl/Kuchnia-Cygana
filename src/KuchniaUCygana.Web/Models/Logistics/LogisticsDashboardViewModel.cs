using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class LogisticsDashboardViewModel
{
    public DateTimeOffset SelectedDate { get; init; }

    public IReadOnlyList<LogisticsDeliveryCandidate> Deliveries { get; init; } = [];

    public IReadOnlyList<DeliveryRouteDto> Routes { get; init; } = [];

    public int PendingAddressesCount { get; init; }

    public int ActiveVehiclesCount { get; init; }

    public decimal ActiveVehiclesCapacityKg { get; init; }

    public int ActiveDriversCount { get; init; }

    public int DriversWithVehicleCount { get; init; }

    public int DeliveriesCount => this.Deliveries.Count;

    public int GeocodedDeliveriesCount => this.Deliveries.Count(delivery => delivery.HasCoordinates);

    public IReadOnlyList<LogisticsDeliveryCandidate> MissingGeocodingDeliveries =>
        this.Deliveries.Where(delivery => !delivery.HasCoordinates).ToList();

    public int RoutesCount => this.Routes.Count;

    public int StopsCount => this.Routes.Sum(route => route.Stops.Count);

    public double TotalDistanceKm => this.Routes.Sum(route => route.TotalDistanceKm);

}
