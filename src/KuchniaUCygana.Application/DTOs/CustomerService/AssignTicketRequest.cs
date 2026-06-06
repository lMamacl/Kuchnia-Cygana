namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class AssignTicketRequest
{
    public int TicketId { get; set; }

    public int AssignedToUserId { get; set; }
}
