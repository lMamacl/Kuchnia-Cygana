using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class CreateTicketRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ClientUserId { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}
