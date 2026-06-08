using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Web.Models;

public sealed class PackingIssueReasonOption
{
    public PackingIncidentReasonFlag Flag { get; init; }

    public string Label { get; init; } = string.Empty;
}

public sealed class PackingItemIssueFormViewModel
{
    public int SessionId { get; set; }

    public int PackingItemId { get; set; }

    public string ClientPublicId { get; set; } = string.Empty;

    public int? DeliveryCalendarId { get; set; }

    public string? BoxCode { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<PackingIncidentReasonFlag> ReasonFlags { get; set; } = new();

    public string? Description { get; set; }

    public IReadOnlyList<PackingIssueReasonOption> ReasonOptions { get; set; } = Array.Empty<PackingIssueReasonOption>();
}

public sealed class PackingBagIssueFormViewModel
{
    public int SessionId { get; set; }

    public int PackingBagId { get; set; }

    public string ClientPublicId { get; set; } = string.Empty;

    public int? DeliveryCalendarId { get; set; }

    public string BagCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int TotalBoxes { get; set; }

    public List<PackingIncidentReasonFlag> ReasonFlags { get; set; } = new();

    public string? Description { get; set; }

    public IReadOnlyList<PackingIssueReasonOption> ReasonOptions { get; set; } = Array.Empty<PackingIssueReasonOption>();
}

public sealed class PackingIncidentListViewModel
{
    public PackingIncidentListFilterViewModel Filter { get; set; } = new();

    public PagedList<PackingIncidentDto> IncidentsPage { get; set; } = new();

    public IReadOnlyList<PackingIncidentDto> Incidents { get; set; } = Array.Empty<PackingIncidentDto>();
}

public sealed class PackingIncidentListFilterViewModel : StaffListFilterViewModel
{
    public DateOnly? Date { get; set; }

    public PackingIncidentStatus? Status { get; set; }

    public PackingIncidentType? Type { get; set; }

    public string? ClientPublicId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        Date.HasValue ||
        Status.HasValue ||
        Type.HasValue ||
        !string.IsNullOrWhiteSpace(ClientPublicId) ||
        DeliveryCalendarId.HasValue;

    public PackingIncidentFilterDto ToSearchRequest()
        => new()
        {
            Date = Date,
            Status = Status,
            Type = Type,
            ClientPublicId = ClientPublicId,
            DeliveryCalendarId = DeliveryCalendarId,
        };

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        if (Date.HasValue)
        {
            values[nameof(Date)] = Date.Value.ToString("yyyy-MM-dd");
        }

        AddIfSet(values, nameof(Status), Status?.ToString());
        AddIfSet(values, nameof(Type), Type?.ToString());
        AddIfSet(values, nameof(ClientPublicId), ClientPublicId);
        AddIfSet(values, nameof(DeliveryCalendarId), DeliveryCalendarId);
        return values;
    }
}

public sealed class KitchenReworkViewModel
{
    public DateOnly SelectedDate { get; set; }

    public IReadOnlyList<PackingIncidentDto> Incidents { get; set; } = Array.Empty<PackingIncidentDto>();
}

public sealed class TransportLabelReprintFormViewModel
{
    public int? SessionId { get; set; }

    public int? RouteId { get; set; }

    public DateOnly? Date { get; set; }

    public string ReprintReason { get; set; } = string.Empty;

    public IReadOnlyList<PackingLabelDto> Labels { get; set; } = Array.Empty<PackingLabelDto>();
}
