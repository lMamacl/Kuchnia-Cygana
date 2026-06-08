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
        return View(await BuildDashboardModelAsync());
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] TicketListFilterViewModel filter)
    {
        SetViewData("Zgloszenia", "Obsluga klienta", "Lista zgloszen z priorytetami, statusem i przypisaniem.");
        return View(await BuildTicketsModelAsync(filter));
    }

    [HttpGet("tickets/new")]
    public async Task<IActionResult> NewTicket()
    {
        SetViewData("Nowe zgloszenie", "Obsluga klienta", "Rejestracja zgloszenia klienta z powiazana dostawa.");
        return View(await BuildNewTicketModelAsync());
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateTicketRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowe zgloszenie", "Obsluga klienta", "Rejestracja zgloszenia klienta z powiazana dostawa.");
            return View("NewTicket", await BuildNewTicketModelAsync(request));
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

    private async Task<CustomerSupportDashboardViewModel> BuildDashboardModelAsync()
    {
        var summary = await customerSupportService.GetTicketDashboardSummaryAsync();
        var queue = await customerSupportService.SearchTicketsAsync(new TicketSearchRequest
        {
            OpenOnly = true,
            QueueOrder = true,
            Page = 1,
            PageSize = 10,
        });
        var unassigned = await customerSupportService.SearchTicketsAsync(new TicketSearchRequest
        {
            OpenOnly = true,
            UnassignedOnly = true,
            QueueOrder = true,
            Page = 1,
            PageSize = 8,
        });

        return new CustomerSupportDashboardViewModel
        {
            Tickets = queue.Items,
            OpenTickets = unassigned.Items,
            Summary = summary,
        };
    }

    private async Task<CustomerSupportDashboardViewModel> BuildTicketsModelAsync(TicketListFilterViewModel filter)
    {
        filter ??= new TicketListFilterViewModel();
        var ticketPage = await customerSupportService.SearchTicketsAsync(new TicketSearchRequest
        {
            Search = filter.Search,
            Status = ParseTicketStatus(filter.Status),
            Priority = ParseTicketPriority(filter.Priority),
            AssignedToUserId = filter.AssignedToUserId is > 0 ? filter.AssignedToUserId : null,
            UnassignedOnly = filter.AssignedToUserId == -1,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        filter.Page = ticketPage.Page;
        filter.PageSize = ticketPage.PageSize;
        var ticketsPageModel = new PagedList<TicketDto>
        {
            Items = ticketPage.Items,
            Page = ticketPage.Page,
            PageSize = ticketPage.PageSize,
            TotalCount = ticketPage.TotalCount,
        };
        var supportUsers = await userService.GetByRolesAsync(BokAssignmentRoles);

        return new CustomerSupportDashboardViewModel
        {
            Tickets = ticketPage.Items,
            Users = supportUsers,
            TicketsPage = ticketsPageModel,
            TicketsFilter = filter,
            OperationalContexts = await customerSupportService.GetOperationalContextsAsync(ticketPage.Items),
        };
    }

    private async Task<CustomerSupportDashboardViewModel> BuildNewTicketModelAsync(CreateTicketRequest? newTicket = null)
    {
        var today = DateTime.Today;
        var deliveryOptions = await customerSupportService.GetDeliveryOptionsAsync(
            today.AddDays(-21),
            today.AddDays(14));
        var clientsPage = await userService.SearchAsync(UserRoles.Client, search: null, page: 1, pageSize: 100);
        var users = clientsPage.Items.ToList();
        if (newTicket?.ClientUserId is > 0 && users.All(user => user.Id != newTicket.ClientUserId))
        {
            var selectedUsers = await userService.GetByIdsAsync([newTicket.ClientUserId]);
            users.AddRange(selectedUsers.Where(user => string.Equals(user.Role, UserRoles.Client, StringComparison.Ordinal)));
        }

        return new CustomerSupportDashboardViewModel
        {
            Users = users,
            DeliveryOptions = deliveryOptions,
            NewTicket = newTicket ?? new CreateTicketRequest(),
        };
    }

    private static TicketStatus? ParseTicketStatus(string? status)
        => Enum.TryParse<TicketStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : null;

    private static TicketPriority? ParseTicketPriority(string? priority)
        => Enum.TryParse<TicketPriority>(priority, ignoreCase: true, out var parsed)
            ? parsed
            : null;

    private void SetViewData(string title, string section, string description)
    {
        ViewData["Title"] = title;
        ViewData["Section"] = section;
        ViewData["Description"] = description;
    }

    private static readonly string[] BokAssignmentRoles = ["BOK", "BOKManager", "Admin"];
    private static readonly string[] BokNotificationRoles = BokAssignmentRoles;
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
