/*
 * Plik: Entities/Admin/Ticket.cs
 * Opis: Encja zgłoszenia (ticket) klienta lub pracownika. Zawiera tytuł, opis, status, priorytet,
 *       przypisanego pracownika oraz datę zamknięcia. Dziedziczy po AuditableEntity.
 */
using System;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Admin;

public sealed class Ticket : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ClientUserId { get; set; }
    public int? AssignedToUserId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public DateTimeOffset? ClosedAt { get; set; }
}
