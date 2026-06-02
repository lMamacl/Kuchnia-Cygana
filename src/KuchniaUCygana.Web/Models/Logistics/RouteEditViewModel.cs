using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class RouteEditViewModel
{
    public UpdateDeliveryRouteRequest Route { get; init; } = new();

    public IReadOnlyList<VehicleDto> Vehicles { get; init; } = [];

    public IReadOnlyList<DriverDto> Drivers { get; init; } = [];

    public IReadOnlyList<RouteStopDto> Stops { get; init; } = [];
}
