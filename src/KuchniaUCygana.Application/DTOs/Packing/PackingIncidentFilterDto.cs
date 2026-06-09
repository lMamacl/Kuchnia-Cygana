using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class PackingIncidentFilterDto
{
    public string? Search { get; set; }

    public DateOnly? Date { get; set; }

    public PackingIncidentStatus? Status { get; set; }

    public PackingIncidentType? Type { get; set; }

    public string? ClientPublicId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public sealed class PackingIncidentPageDto
{
    public IReadOnlyList<PackingIncidentDto> Items { get; init; } = Array.Empty<PackingIncidentDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
