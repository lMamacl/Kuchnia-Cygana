using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class RoutesViewModel
{
    public DateTimeOffset SelectedDate { get; init; }

    public IReadOnlyList<DeliveryRouteDto> Routes { get; init; } = [];

    public DailyRouteGenerationResultDto? GenerationResult { get; init; }

    public int TotalStops => this.Routes.Sum(route => route.Stops.Count);

    public int AssignedVehiclesCount => this.Routes.Count(route => route.VehicleId.HasValue);

    public double TotalDistanceKm => this.Routes.Sum(route => route.TotalDistanceKm);
}
