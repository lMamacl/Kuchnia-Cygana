using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = UserRoles.Driver)]
[Route("driver")]
public sealed class DriverMobileController : Controller
{
    private readonly IDriverMobileService driverMobileService;

    public DriverMobileController(IDriverMobileService driverMobileService)
    {
        this.driverMobileService = driverMobileService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetTitle("Moja trasa");
        return this.View(await this.driverMobileService.GetDashboardAsync());
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartRoute()
    {
        try
        {
            await this.driverMobileService.StartRouteAsync();
            this.TempData["Success"] = "Trasa zostala rozpoczeta.";
        }
        catch (InvalidOperationException exception)
        {
            this.TempData["Error"] = exception.Message;
        }

        return this.RedirectToAction(nameof(this.Index));
    }

    [HttpGet("stop/{id:int}")]
    public async Task<IActionResult> Stop(int id)
    {
        SetTitle("Szczegoly dostawy");
        var stop = await this.driverMobileService.GetStopAsync(id);
        return stop is null ? this.NotFound() : this.View(stop);
    }

    [HttpPost("stop/{id:int}/start")]
    public async Task<IActionResult> StartStop(int id)
    {
        try
        {
            await this.driverMobileService.StartStopAsync(id);
            this.TempData["Success"] = "Rozpoczeto obsluge przystanku.";
        }
        catch (InvalidOperationException exception)
        {
            this.TempData["Error"] = exception.Message;
        }

        return this.RedirectToAction(nameof(this.Stop), new { id });
    }

    [HttpGet("stop/{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id)
    {
        SetTitle("Potwierdzenie dostawy");
        var stop = await this.driverMobileService.GetStopAsync(id);
        return stop is null ? this.NotFound() : this.View(stop);
    }

    [HttpPost("stop/{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, ConfirmDriverDeliveryRequest request)
    {
        try
        {
            await this.driverMobileService.ConfirmDeliveryAsync(id, request);
            this.TempData["Success"] = "Dostawa zostala potwierdzona.";
            return this.RedirectToAction(nameof(this.Index));
        }
        catch (InvalidOperationException exception)
        {
            this.TempData["Error"] = exception.Message;
            return this.RedirectToAction(nameof(this.Confirm), new { id });
        }
    }

    [HttpGet("stop/{id:int}/problem")]
    public async Task<IActionResult> Problem(int id)
    {
        SetTitle("Problem z dostawa");
        var stop = await this.driverMobileService.GetStopAsync(id);
        return stop is null ? this.NotFound() : this.View(stop);
    }

    [HttpPost("stop/{id:int}/problem")]
    public async Task<IActionResult> Problem(int id, ReportDriverDeliveryProblemRequest request)
    {
        try
        {
            await this.driverMobileService.ReportProblemAsync(id, request);
            this.TempData["Success"] = "Problem zostal zapisany.";
            return this.RedirectToAction(nameof(this.Index));
        }
        catch (InvalidOperationException exception)
        {
            this.TempData["Error"] = exception.Message;
            return this.RedirectToAction(nameof(this.Problem), new { id });
        }
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        SetTitle("Podsumowanie dnia");
        return this.View(await this.driverMobileService.GetSummaryAsync());
    }

    private void SetTitle(string title)
    {
        this.ViewData["Title"] = title;
    }
}
