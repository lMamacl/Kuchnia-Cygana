namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class VehicleSearchRequest
{
    public string? Search { get; init; }

    public string? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public sealed class VehiclePageDto
{
    public IReadOnlyList<VehicleDto> Items { get; init; } = Array.Empty<VehicleDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalFleetCount { get; init; }

    public int ActiveCount { get; init; }

    public int MaintenanceCount { get; init; }

    public decimal ActiveCapacityKg { get; init; }
}

public sealed class DriverSearchRequest
{
    public string? Search { get; init; }

    public bool? IsActive { get; init; }

    public bool? HasVehicleAssignment { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public sealed class DriverPageDto
{
    public IReadOnlyList<DriverDto> Items { get; init; } = Array.Empty<DriverDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalDriversCount { get; init; }

    public int ActiveCount { get; init; }

    public int WithVehicleCount { get; init; }

    public int InactiveCount { get; init; }
}
