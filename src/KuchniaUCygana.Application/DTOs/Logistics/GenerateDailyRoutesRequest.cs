namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class GenerateDailyRoutesRequest
{
    public DateTimeOffset RouteDate { get; init; } = DateTimeOffset.Now;

    public decimal DefaultDeliveryLoadKg { get; init; } = 1m;

    public int? MaxStopsPerRoute { get; init; }
}
