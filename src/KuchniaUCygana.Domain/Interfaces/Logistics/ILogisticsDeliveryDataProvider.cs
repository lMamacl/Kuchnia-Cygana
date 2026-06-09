namespace KuchniaUCygana.Domain.Interfaces.Logistics;

public interface ILogisticsDeliveryDataProvider
{
    Task<IReadOnlyList<LogisticsDeliveryCandidate>> GetDeliveriesForDateAsync(
        DateTime deliveryDate,
        decimal defaultDeliveryLoadKg = 1m);

    Task<IReadOnlyList<LogisticsDeliveryCandidate>> GetDeliveriesByCalendarIdsAsync(
        IReadOnlyCollection<int> deliveryCalendarIds,
        decimal defaultDeliveryLoadKg = 1m);
}

public sealed class LogisticsDeliveryCandidate
{
    public int DeliveryCalendarId { get; init; }

    public int OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public DateTime DeliveryDate { get; init; }

    public string FullAddress { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public decimal EstimatedLoadKg { get; init; }

    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
}
