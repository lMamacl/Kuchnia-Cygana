using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class ChangeTicketStatusRequest
{
    public int TicketId { get; set; }

    public TicketStatus Status { get; set; }
}
