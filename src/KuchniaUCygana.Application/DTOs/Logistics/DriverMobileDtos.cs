namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class DriverDashboardDto
{
    public DateOnly DeliveryDate { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public string? SetupMessage { get; set; }

    public DriverVehicleDto? Vehicle { get; set; }

    public DriverRouteDto? Route { get; set; }
}

public sealed class DriverVehicleDto
{
    public int Id { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}

public sealed class DriverRouteDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly DeliveryDate { get; set; }

    public double TotalDistanceKm { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsReadyForDeparture { get; set; }

    public string ReadinessMessage { get; set; } = string.Empty;

    public int CompletedStops { get; set; }

    public int FailedStops { get; set; }

    public List<DriverRouteStopDto> Stops { get; set; } = new();

    public int TotalStops => this.Stops.Count;

    public int HandledStops => this.CompletedStops + this.FailedStops;

    public int ProgressPercent => this.TotalStops == 0
        ? 0
        : (int)Math.Round(this.HandledStops * 100d / this.TotalStops);

    public bool CanStart => this.Status == "Assigned" && this.IsReadyForDeparture;

    public bool IsTerminal => this.Status is "Completed" or "Failed";
}

public sealed class DriverRouteStopDto
{
    public int Id { get; set; }

    public int DeliveryCalendarId { get; set; }

    public int SequenceNumber { get; set; }

    public string FullAddress { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset? PlannedArrivalTime { get; set; }

    public DateTimeOffset? ActualArrivalTime { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? OrderNumber { get; set; }

    public List<DriverDeliveryBagDto> Bags { get; set; } = new();

    public DriverDeliveryIssueDto? LatestIssue { get; set; }

    public bool IsNext { get; set; }

    public bool CanStart { get; set; }

    public bool CanConfirm { get; set; }

    public bool CanReportProblem { get; set; }

    public bool IsTerminal => this.Status is "Completed" or "Failed";
}

public sealed class DriverDeliveryBagDto
{
    public int Id { get; set; }

    public int BagNumber { get; set; }

    public string BagCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public sealed class DriverDeliveryIssueDto
{
    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset ReportedAt { get; set; }
}

public sealed class DriverRouteSummaryDto
{
    public DateOnly DeliveryDate { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public DriverVehicleDto? Vehicle { get; set; }

    public DriverRouteDto? Route { get; set; }

    public string? SetupMessage { get; set; }
}

public sealed class ConfirmDriverDeliveryRequest
{
    public List<string> BagCodes { get; set; } = new();
}

public sealed class ReportDriverDeliveryProblemRequest
{
    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
