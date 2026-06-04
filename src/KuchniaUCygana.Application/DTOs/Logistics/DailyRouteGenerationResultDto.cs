namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class DailyRouteGenerationResultDto
{
    public bool Succeeded { get; init; }

    public int GeneratedRoutesCount { get; init; }

    public int PlannedStopsCount { get; init; }

    public List<DeliveryRouteDto> Routes { get; init; } = new();

    public List<RouteGenerationIssueDto> Issues { get; init; } = new();

    public List<RouteDeliveryCandidateDto> MissingGeocoding { get; init; } = new();

    public List<RouteDeliveryCandidateDto> UnassignedDeliveries { get; init; } = new();
}

public sealed class RouteGenerationIssueDto
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public sealed class RouteDeliveryCandidateDto
{
    public int DeliveryCalendarId { get; init; }

    public int OrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string FullAddress { get; init; } = string.Empty;

    public decimal EstimatedLoadKg { get; init; }
}
