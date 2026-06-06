using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Application.Interfaces;
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
    private readonly IUserService userService;

    public CustomerSupportController(
        ICustomerSupportService customerSupportService,
        IUserService userService)
    {
        this.customerSupportService = customerSupportService;
        this.userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetViewData("BOK", "Obsluga klienta", "Dashboard zgloszen klientow i kolejki pracy.");
        return View(await BuildModelAsync());
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets()
    {
        SetViewData("Zgloszenia", "Obsluga klienta", "Lista zgloszen z priorytetami, statusem i przypisaniem.");
        return View(await BuildModelAsync());
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateTicketRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Zgloszenia", "Obsluga klienta", "Lista zgloszen z priorytetami, statusem i przypisaniem.");
            return View("Tickets", await BuildModelAsync(newTicket: request));
        }

        try
        {
            await customerSupportService.CreateTicketAsync(request);
            TempData["Success"] = "Zgloszenie zostalo dodane.";
            return RedirectToAction(nameof(Tickets));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Tickets));
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
        return RedirectToAction(nameof(Tickets));
    }

    private async Task<CustomerSupportDashboardViewModel> BuildModelAsync(CreateTicketRequest? newTicket = null)
    {
        return new CustomerSupportDashboardViewModel
        {
            Tickets = (await customerSupportService.GetTicketsAsync()).ToArray(),
            OpenTickets = (await customerSupportService.GetOpenTicketsAsync()).ToArray(),
            Users = (await userService.GetAllAsync()).ToArray(),
            NewTicket = newTicket ?? new CreateTicketRequest(),
        };
    }

    private void SetViewData(string title, string section, string description)
    {
        ViewData["Title"] = title;
        ViewData["Section"] = section;
        ViewData["Description"] = description;
    }
}
