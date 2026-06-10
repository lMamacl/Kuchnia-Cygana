namespace KuchniaUCygana.Domain.Interfaces.Logistics;

public interface ILogisticsSbdReportRepository
{
    Task<LogisticsDailyDispatchBoardReport> GetDailyDispatchBoardAsync(
        DateTime deliveryDate,
        decimal estimatedDeliveryWeightKg);
}

public sealed class LogisticsDailyDispatchBoardReport
{
    public IReadOnlyList<LogisticsDailyDispatchRouteRow> Routes { get; init; } = [];

    public LogisticsDailyDispatchSummaryRow Summary { get; init; } = new();
}

public sealed class LogisticsDailyDispatchRouteRow
{
    public DateTime RouteDate { get; set; }

    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int RouteStatus { get; set; }

    public string RouteStatusName { get; set; } = string.Empty;

    public string? VehicleRegistrationNumber { get; set; }

    public string? VehicleModel { get; set; }

    public decimal? VehicleMaxLoadKg { get; set; }

    public int? DriverId { get; set; }

    public string DriverDisplayName { get; set; } = string.Empty;

    public int StopCount { get; set; }

    public int CompletedStopCount { get; set; }

    public int FailedStopCount { get; set; }

    public int CityCount { get; set; }

    public int UnlinkedStopCount { get; set; }

    public int MissingCoordinatesCount { get; set; }

    public decimal EstimatedLoadKg { get; set; }

    public decimal? EstimatedLoadPercent { get; set; }

    public bool HasManifest { get; set; }

    public string? ManifestNumber { get; set; }

    public string ManifestStatusName { get; set; } = string.Empty;

    public DateTimeOffset? SentToLogisticsAt { get; set; }

    public bool RequiresRegeneration { get; set; }
}

public sealed class LogisticsDailyDispatchSummaryRow
{
    public DateTime DeliveryDate { get; set; }

    public int RouteCount { get; set; }

    public int StopCount { get; set; }

    public int CompletedStopCount { get; set; }

    public int FailedStopCount { get; set; }

    public int UnlinkedStopCount { get; set; }

    public int MissingCoordinatesCount { get; set; }

    public decimal EstimatedLoadKg { get; set; }

    public int RoutesWithManifest { get; set; }

    public int RoutesSentToLogistics { get; set; }

    public int RoutesRequiringRegeneration { get; set; }
}
