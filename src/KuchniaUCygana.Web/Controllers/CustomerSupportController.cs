using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "BOK,BOKManager,Admin")]
[Route("bok")]
public sealed class CustomerSupportController : Controller
{
    private readonly ICustomerSupportService customerSupportService;
    private readonly IStaffActivityService staffActivityService;
    private readonly IUserService userService;

    public CustomerSupportController(
        ICustomerSupportService customerSupportService,
        IStaffActivityService staffActivityService,
        IUserService userService)
    {
        this.customerSupportService = customerSupportService;
        this.staffActivityService = staffActivityService;
        this.userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetViewData("BOK", "Obsluga klienta", "Dashboard zgloszen klientow i kolejki pracy.");
        return View(await BuildModelAsync());
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] TicketListFilterViewModel filter)
    {
        SetViewData("Zgloszenia", "Obsluga klienta", "Lista zgloszen z priorytetami, statusem i przypisaniem.");
        return View(await BuildModelAsync(ticketsFilter: filter));
    }

    [HttpGet("tickets/new")]
    public async Task<IActionResult> NewTicket()
    {
        SetViewData("Nowe zgloszenie", "Obsluga klienta", "Rejestracja zgloszenia klienta z powiazana dostawa.");
        return View(await BuildModelAsync());
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateTicketRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowe zgloszenie", "Obsluga klienta", "Rejestracja zgloszenia klienta z powiazana dostawa.");
            return View("NewTicket", await BuildModelAsync(newTicket: request));
        }

        try
        {
            var ticket = await customerSupportService.CreateTicketAsync(request);
            await staffActivityService.RecordAsync(
                "BOK.CreateTicket",
                "Ticket",
                ticket.Id.ToString(),
                newValue: new
                {
                    ticket.Id,
                    ticket.Title,
                    ticket.ClientUserId,
                    ticket.ClientFullName,
                    ticket.OrderId,
                    ticket.DeliveryCalendarId,
                    ticket.Priority,
                    ticket.Status,
                },
                notification: BuildNotification(
                    "BOK",
                    ticket.Priority is "High" or "Critical" ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    "Nowe zgloszenie BOK",
                    $"{ticket.ClientFullName ?? $"Klient #{ticket.ClientUserId}"}: {ticket.Title}",
                    "/bok/tickets",
                    "Ticket",
                    ticket.Id),
                notifyRoles: BokNotificationRoles);
            TempData["Success"] = "Zgloszenie zostalo dodane.";
            return RedirectToAction(nameof(Tickets));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(NewTicket));
        }
    }

    [HttpPost("tickets/{id:int}/assign")]
    public async Task<IActionResult> AssignTicket(int id, int assignedToUserId)
    {
        try
        {
            var ticket = await customerSupportService.AssignTicketAsync(new AssignTicketRequest
            {
                TicketId = id,
                AssignedToUserId = assignedToUserId,
            });

            TempData[ticket is null ? "Error" : "Success"] = ticket is null
                ? "Nie znaleziono zgloszenia."
                : "Zgloszenie zostalo przypisane.";
            if (ticket is not null)
            {
                await staffActivityService.RecordAsync(
                    "BOK.AssignTicket",
                    "Ticket",
                    ticket.Id.ToString(),
                    newValue: new
                    {
                        ticket.Id,
                        ticket.Title,
                        ticket.AssignedToUserId,
                        ticket.AssignedToFullName,
                        ticket.Status,
                    },
                    notification: BuildNotification(
                        "BOK",
                        NotificationSeverity.Info,
                        "Przypisano zgloszenie",
                        $"{ticket.Title} przypisano do {ticket.AssignedToFullName ?? $"uzytkownika #{ticket.AssignedToUserId}"}.",
                        "/bok/tickets",
                        "Ticket",
                        ticket.Id),
                    notifyRoles: BokNotificationRoles);
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Tickets));
    }

    [HttpPost("tickets/{id:int}/status")]
    public async Task<IActionResult> ChangeTicketStatus(int id, TicketStatus status)
    {
        var ticket = await customerSupportService.ChangeTicketStatusAsync(new ChangeTicketStatusRequest
        {
            TicketId = id,
            Status = status,
        });

        TempData[ticket is null ? "Error" : "Success"] = ticket is null
            ? "Nie znaleziono zgloszenia."
            : "Status zgloszenia zostal zmieniony.";
        if (ticket is not null)
        {
            await staffActivityService.RecordAsync(
                "BOK.ChangeTicketStatus",
                "Ticket",
                ticket.Id.ToString(),
                newValue: new
                {
                    ticket.Id,
                    ticket.Title,
                    ticket.Status,
                    ticket.ClosedAt,
                },
                notification: BuildNotification(
                    "BOK",
                    ticket.Status is "Resolved" or "Closed" ? NotificationSeverity.Success : NotificationSeverity.Info,
                    "Zmieniono status zgloszenia",
                    $"{ticket.Title}: {ticket.Status}.",
                    "/bok/tickets",
                    "Ticket",
                    ticket.Id),
                notifyRoles: BokNotificationRoles);
        }

        return RedirectToAction(nameof(Tickets));
    }

    private async Task<CustomerSupportDashboardViewModel> BuildModelAsync(
        CreateTicketRequest? newTicket = null,
        TicketListFilterViewModel? ticketsFilter = null)
    {
        var tickets = (await customerSupportService.GetTicketsAsync()).ToArray();
        var openTickets = (await customerSupportService.GetOpenTicketsAsync()).ToArray();
        var users = (await userService.GetAllAsync()).ToArray();
        ticketsFilter ??= new TicketListFilterViewModel();
        var ticketsPageModel = PagedList<TicketDto>.Create(
            FilterTickets(tickets, ticketsFilter).OrderByDescending(ticket => ticket.CreatedAt),
            ticketsFilter.Page,
            ticketsFilter.PageSize);
        ticketsFilter.Page = ticketsPageModel.Page;
        ticketsFilter.PageSize = ticketsPageModel.PageSize;
        var today = DateTime.Today;
        var deliveryOptions = await customerSupportService.GetDeliveryOptionsAsync(
            today.AddDays(-21),
            today.AddDays(14));

        return new CustomerSupportDashboardViewModel
        {
            Tickets = tickets,
            OpenTickets = openTickets,
            Users = users,
            TicketsPage = ticketsPageModel,
            TicketsFilter = ticketsFilter,
            OperationalContexts = await customerSupportService.GetOperationalContextsAsync(ticketsPageModel.Items),
            DeliveryOptions = deliveryOptions,
            NewTicket = newTicket ?? new CreateTicketRequest(),
        };
    }

    private static IEnumerable<TicketDto> FilterTickets(
        IEnumerable<TicketDto> tickets,
        TicketListFilterViewModel filter)
    {
        var query = tickets;
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(ticket => string.Equals(ticket.Status, filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            query = query.Where(ticket => string.Equals(ticket.Priority, filter.Priority, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.AssignedToUserId == -1)
        {
            query = query.Where(ticket => !ticket.AssignedToUserId.HasValue);
        }
        else if (filter.AssignedToUserId is > 0)
        {
            query = query.Where(ticket => ticket.AssignedToUserId == filter.AssignedToUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(ticket => MatchesSearch(
                filter.Search,
                ticket.Title,
                ticket.Description,
                ticket.ClientFullName,
                ticket.AssignedToFullName,
                ticket.Status,
                ticket.Priority,
                ticket.Id.ToString(),
                ticket.ClientUserId.ToString(),
                ticket.OrderId?.ToString(),
                ticket.DeliveryCalendarId?.ToString()));
        }

        return query;
    }

    private static bool MatchesSearch(string? search, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var normalizedSearch = search.Trim();
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
    }

    private void SetViewData(string title, string section, string description)
    {
        ViewData["Title"] = title;
        ViewData["Section"] = section;
        ViewData["Description"] = description;
    }

    private static readonly string[] BokNotificationRoles = ["BOK", "BOKManager", "Admin"];

    private static Notification BuildNotification(
        string type,
        string severity,
        string title,
        string message,
        string linkUrl,
        string sourceType,
        long sourceId)
        => new()
        {
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            SourceType = sourceType,
            SourceId = sourceId,
        };
}
