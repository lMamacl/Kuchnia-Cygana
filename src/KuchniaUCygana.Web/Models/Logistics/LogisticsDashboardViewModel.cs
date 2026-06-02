using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class LogisticsDashboardViewModel
{
    public DateTimeOffset SelectedDate { get; init; }

    public IReadOnlyList<LogisticsDeliveryCandidate> Deliveries { get; init; } = [];

    public IReadOnlyList<DeliveryRouteDto> Routes { get; init; } = [];

    public IReadOnlyList<VehicleDto> Vehicles { get; init; } = [];

    public IReadOnlyList<DriverDto> Drivers { get; init; } = [];

    public int PendingAddressesCount { get; init; }

    public int DeliveriesCount => this.Deliveries.Count;

    public int GeocodedDeliveriesCount => this.Deliveries.Count(delivery => delivery.HasCoordinates);

    public IReadOnlyList<LogisticsDeliveryCandidate> MissingGeocodingDeliveries =>
        this.Deliveries.Where(delivery => !delivery.HasCoordinates).ToList();

    public int RoutesCount => this.Routes.Count;

    public int StopsCount => this.Routes.Sum(route => route.Stops.Count);

    public double TotalDistanceKm => this.Routes.Sum(route => route.TotalDistanceKm);

    public int ActiveVehiclesCount =>
        this.Vehicles.Count(vehicle => vehicle.Status == VehicleStatus.Active.ToString());

    public decimal ActiveVehiclesCapacityKg =>
        this.Vehicles
            .Where(vehicle => vehicle.Status == VehicleStatus.Active.ToString())
            .Sum(vehicle => vehicle.MaxLoadKg);

    public int ActiveDriversCount => this.Drivers.Count(driver => driver.IsActive);

    public int DriversWithVehicleCount => this.Drivers.Count(driver => driver.HasVehicleAssignment);
}
