using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class UpdateTicketRequest
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? OrderId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int? AssignedToUserId { get; set; }

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }
}
