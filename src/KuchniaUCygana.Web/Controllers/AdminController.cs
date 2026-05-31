using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController : Controller
{
    private readonly IPackingIncidentService packingIncidentService;

    public AdminController(IPackingIncidentService packingIncidentService)
    {
        this.packingIncidentService = packingIncidentService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Admin";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Dashboard administracyjny systemu.";
        return View();
    }

    [HttpGet("users")]
    public IActionResult Users()
    {
        ViewData["Title"] = "Uzytkownicy";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie uzytkownikami.";
        return View();
    }

    [HttpGet("roles")]
    public IActionResult Roles()
    {
        ViewData["Title"] = "Role";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie rolami i uprawnieniami.";
        return View();
    }

    [HttpGet("settings")]
    public IActionResult Settings()
    {
        ViewData["Title"] = "Ustawienia";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Ustawienia systemowe.";
        return View();
    }

    [HttpGet("packing-incidents")]
    public async Task<IActionResult> PackingIncidents(PackingIncidentFilterDto filter)
    {
        ViewData["Title"] = "Awarie kompletacji";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Obsługa zgłoszeń z kompletacji pudełek i toreb.";

        return View(new PackingIncidentListViewModel
        {
            Filter = filter,
            Incidents = await packingIncidentService.SearchAsync(filter),
        });
    }

    [HttpPost("packing-incidents/{incidentId:int}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPackingIncident(int incidentId)
    {
        await packingIncidentService.AssignToCurrentUserAsync(incidentId);
        TempData["Success"] = $"Przypisano zgłoszenie #{incidentId}.";
        return RedirectToAction(nameof(PackingIncidents));
    }

    [HttpPost("packing-incidents/{incidentId:int}/note")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPackingIncidentNote(int incidentId, string? notes)
    {
        await packingIncidentService.AddAdminNoteAsync(new HandlePackingIncidentRequest
        {
            IncidentId = incidentId,
            Notes = notes,
        });
        TempData["Success"] = $"Dodano notatkę do zgłoszenia #{incidentId}.";
        return RedirectToAction(nameof(PackingIncidents));
    }

    [HttpPost("packing-incidents/{incidentId:int}/waste")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPackingIncidentWaste(int incidentId, string? notes)
    {
        try
        {
            await packingIncidentService.RegisterWasteAsync(new RegisterIncidentWasteRequest
            {
                IncidentId = incidentId,
                Notes = notes,
            });
            TempData["Success"] = $"Zarejestrowano rozchód magazynowy dla zgłoszenia #{incidentId}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(PackingIncidents));
    }

    [HttpPost("packing-incidents/{incidentId:int}/rework")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPackingIncidentRework(int incidentId, string? notes)
    {
        try
        {
            await packingIncidentService.RequestKitchenReworkAsync(new HandlePackingIncidentRequest
            {
                IncidentId = incidentId,
                Notes = notes,
            });
            TempData["Success"] = $"Ponowiono zadanie kuchni dla zgłoszenia #{incidentId}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(PackingIncidents));
    }

    [HttpPost("packing-incidents/{incidentId:int}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolvePackingIncident(int incidentId, string? resolutionNotes)
    {
        await packingIncidentService.ResolveAsync(new ResolvePackingIncidentRequest
        {
            IncidentId = incidentId,
            ResolutionNotes = resolutionNotes,
        });
        TempData["Success"] = $"Zamknięto zgłoszenie #{incidentId}.";
        return RedirectToAction(nameof(PackingIncidents));
    }
}

