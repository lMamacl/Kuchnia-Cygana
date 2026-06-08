namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketDashboardSummaryDto
{
    public int WaitingCount { get; init; }

    public int UnassignedCount { get; init; }

    public int HighPriorityCount { get; init; }

    public int ClosedTodayCount { get; init; }
}
