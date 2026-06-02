/*
 * Plik: Enums/TicketStatus.cs
 * Opis: Enum stanów zgłoszenia: Open, InProgress, Resolved, Closed, Reopened.
 */
namespace KuchniaUCygana.Domain.Enums;

public enum TicketStatus
{
    New = 0,
    Open = 1,
    Pending = 2,
    Resolved = 3,
    Closed = 4
}
