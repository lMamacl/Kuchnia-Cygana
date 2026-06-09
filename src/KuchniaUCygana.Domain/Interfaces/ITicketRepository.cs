/*
 * Plik: Interfaces/ITicketRepository.cs
 * Opis: Kontrakt dla repozytorium ticketów – rozszerza IRepository o specyficzne zapytania: 
 *       GetByClientIdAsync, GetByStatusAsync, GetOpenTicketsAsync.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces;

public interface ITicketRepository : IRepository<Ticket>
{
    Task<IEnumerable<Ticket>> GetByClientIdAsync(int clientUserId);
    Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status);
    Task<TicketSearchResult> SearchAsync(TicketSearchQuery query);
    Task<TicketDashboardSummary> GetDashboardSummaryAsync(DateTimeOffset closedFromInclusive, DateTimeOffset closedToExclusive);
}

public sealed record TicketSearchQuery(
    string? Search,
    TicketStatus? Status,
    TicketPriority? Priority,
    int? AssignedToUserId,
    bool UnassignedOnly,
    bool OpenOnly,
    bool QueueOrder,
    int Page,
    int PageSize);

public sealed class TicketSearchResult
{
    public IReadOnlyList<TicketSearchRow> Items { get; init; } = Array.Empty<TicketSearchRow>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class TicketSearchRow
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ClientUserId { get; set; }

    public string? ClientFullName { get; set; }

    public int? OrderId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int? AssignedToUserId { get; set; }

    public string? AssignedToFullName { get; set; }

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class TicketDashboardSummary
{
    public int WaitingCount { get; set; }

    public int UnassignedCount { get; set; }

    public int HighPriorityCount { get; set; }

    public int ClosedTodayCount { get; set; }
}
