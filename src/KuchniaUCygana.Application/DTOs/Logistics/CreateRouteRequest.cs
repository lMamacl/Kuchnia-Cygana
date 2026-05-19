namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class CreateRouteRequest
{
    public DateTimeOffset RouteDate { get; init; }
    public List<int> DeliveryCalendarIds { get; init; } = new();
}