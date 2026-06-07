using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.CustomerService;

namespace KuchniaUCygana.Web.Models;

public sealed class CustomerSupportDashboardViewModel
{
    public IReadOnlyList<TicketDto> Tickets { get; init; } = [];

    public IReadOnlyList<TicketDto> OpenTickets { get; init; } = [];

    public IReadOnlyList<UserDto> Users { get; init; } = [];

    public PagedList<TicketDto> TicketsPage { get; init; } = new();

    public IReadOnlyDictionary<int, TicketOperationalContextDto> OperationalContexts { get; init; }
        = new Dictionary<int, TicketOperationalContextDto>();

    public IReadOnlyList<TicketDeliveryOptionDto> DeliveryOptions { get; init; } = [];

    public CreateTicketRequest NewTicket { get; init; } = new();
}
