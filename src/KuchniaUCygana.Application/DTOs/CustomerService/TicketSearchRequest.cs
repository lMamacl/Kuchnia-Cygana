using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketSearchRequest
{
    public string? Search { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public int? AssignedToUserId { get; init; }

    public bool UnassignedOnly { get; init; }

    public bool OpenOnly { get; init; }

    public bool QueueOrder { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
