using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Warehouse;
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
    public PackingIncidentFilterDto Filter { get; set; } = new();

    public IReadOnlyList<PackingIncidentDto> Incidents { get; set; } = Array.Empty<PackingIncidentDto>();
}

public sealed class KitchenReworkViewModel
{
    public KitchenReworkFilterViewModel Filter { get; set; } = new();

    public DateOnly SelectedDate { get; set; }

    public IReadOnlyList<PackingIncidentDto> Incidents { get; set; } = Array.Empty<PackingIncidentDto>();

    public PagedResultDto<PackingIncidentDto> Page { get; set; } = new();
}

public sealed class KitchenReworkFilterViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string? Search { get; set; }

    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class TransportLabelReprintFormViewModel
{
    public int? SessionId { get; set; }

    public int? RouteId { get; set; }

    public DateOnly? Date { get; set; }

    public string ReprintReason { get; set; } = string.Empty;

    public IReadOnlyList<PackingLabelDto> Labels { get; set; } = Array.Empty<PackingLabelDto>();
}
