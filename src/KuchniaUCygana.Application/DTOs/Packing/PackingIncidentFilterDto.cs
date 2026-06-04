using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class PackingIncidentFilterDto
{
    public DateOnly? Date { get; set; }

    public PackingIncidentStatus? Status { get; set; }

    public PackingIncidentType? Type { get; set; }

    public string? ClientPublicId { get; set; }

    public int? DeliveryCalendarId { get; set; }
}
