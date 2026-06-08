using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.CustomerService;

namespace KuchniaUCygana.Web.Models;

public sealed class CustomerSupportDashboardViewModel
{
    public IReadOnlyList<TicketDto> Tickets { get; init; } = [];

    public IReadOnlyList<TicketDto> OpenTickets { get; init; } = [];

    public TicketDashboardSummaryDto Summary { get; init; } = new();

    public IReadOnlyList<UserDto> Users { get; init; } = [];

    public PagedList<TicketDto> TicketsPage { get; init; } = new();

    public TicketListFilterViewModel TicketsFilter { get; init; } = new();

    public IReadOnlyDictionary<int, TicketOperationalContextDto> OperationalContexts { get; init; }
        = new Dictionary<int, TicketOperationalContextDto>();

    public IReadOnlyList<TicketDeliveryOptionDto> DeliveryOptions { get; init; } = [];

    public CreateTicketRequest NewTicket { get; init; } = new();
}

public sealed class TicketListFilterViewModel : StaffListFilterViewModel
{
    public string? Status { get; set; }

    public string? Priority { get; set; }

    public int? AssignedToUserId { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Priority) ||
        AssignedToUserId.HasValue;

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        AddIfSet(values, nameof(Status), Status);
        AddIfSet(values, nameof(Priority), Priority);
        AddIfSet(values, nameof(AssignedToUserId), AssignedToUserId);
        return values;
    }
}
