using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

/// <summary>
/// Kontroler magazynu — stany, przyjęcia, odpisy, inwentaryzacja, temperatury, alerty.
/// TASK-M3-025 | Role: Kitchen, Admin
/// </summary>
[Authorize(Roles = "Kitchen,Admin")]
[Route("warehouse")]
public sealed class WarehouseController : Controller
{
    private readonly IWarehouseService warehouseService;
    private readonly ITemperatureService temperatureService;

    public WarehouseController(
        IWarehouseService warehouseService,
        ITemperatureService temperatureService)
    {
        this.warehouseService = warehouseService;
        this.temperatureService = temperatureService;
    }

    // ── Stany magazynowe ─────────────────────────────────────

    /// <summary>
    /// Widok główny magazynu — przegląd stanów z alertami.
    /// GET /warehouse
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var alerts = await warehouseService.GetSmartAlertsAsync();
        return View(alerts);
    }

    /// <summary>
    /// Endpoint HTMX — lista alertów (partial).
    /// GET /warehouse/alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var alerts = await warehouseService.GetSmartAlertsAsync();
        return PartialView("_AlertsPartial", alerts);
    }

    // ── Przyjęcie dostawy ────────────────────────────────────

    /// <summary>
    /// Formularz przyjęcia dostawy.
    /// GET /warehouse/receive
    /// </summary>
    [HttpGet("receive")]
    public IActionResult Receive()
    {
        return View(new ReceiveDeliveryRequest());
    }

    /// <summary>
    /// Przetwarza przyjęcie dostawy — tworzy nową partię.
    /// POST /warehouse/receive
    /// </summary>
    [HttpPost("receive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(ReceiveDeliveryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        var batch = await warehouseService.ReceiveDeliveryAsync(request);
        TempData["Success"] = $"Dostawa przyjęta. Partia #{batch.Id} ({batch.CurrentQuantity}) zarejestrowana.";
        return RedirectToAction(nameof(Index));
    }

    // ── Rejestracja odpadu ───────────────────────────────────

    /// <summary>
    /// Formularz rejestracji straty/odpadu.
    /// GET /warehouse/waste
    /// </summary>
    [HttpGet("waste")]
    public IActionResult Waste()
    {
        return View(new RegisterWasteRequest());
    }

    /// <summary>
    /// Przetwarza rejestrację odpadu — zdejmuje ze stanu wg FEFO.
    /// POST /warehouse/waste
    /// </summary>
    [HttpPost("waste")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Waste(RegisterWasteRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        await warehouseService.RegisterWasteAsync(request);
        TempData["Success"] = "Odpad zarejestrowany i zdjęty z magazynu.";
        return RedirectToAction(nameof(Index));
    }

    // ── Inwentaryzacja ───────────────────────────────────────

    /// <summary>
    /// Formularz inwentaryzacyjny — lista składników do przeliczenia.
    /// GET /warehouse/inventory
    /// </summary>
    [HttpGet("inventory")]
    public IActionResult Inventory()
    {
        return View();
    }

    /// <summary>
    /// Przetwarza wyniki inwentaryzacji — korekty stanów.
    /// POST /warehouse/inventory
    /// </summary>
    [HttpPost("inventory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inventory(List<StockItemAdjustment> adjustments)
    {
        if (!ModelState.IsValid)
        {
            return View();
        }

        await warehouseService.PerformInventoryAsync(adjustments);
        TempData["Success"] = $"Inwentaryzacja zakończona. Skorygowano {adjustments.Count} pozycji.";
        return RedirectToAction(nameof(Index));
    }

    // ── Temperatury / HACCP ──────────────────────────────────

    /// <summary>
    /// Widok logowania temperatur i historii.
    /// GET /warehouse/temperatures
    /// </summary>
    [HttpGet("temperatures")]
    public IActionResult Temperatures()
    {
        return View();
    }

    /// <summary>
    /// Loguje odczyt temperatury.
    /// POST /warehouse/temperatures
    /// </summary>
    [HttpPost("temperatures")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTemperature(LogTemperatureRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Temperatures", request);
        }

        var log = await temperatureService.LogTemperatureAsync(request);
        TempData["Success"] = $"Temperatura {log.RecordedTemperatureCelsius}°C zapisana ({log.DeviceNameOrLocation}).";
        return RedirectToAction(nameof(Temperatures));
    }

    /// <summary>
    /// Generuje raport HACCP za wybrany zakres dat.
    /// GET /warehouse/haccp-report?from=2026-05-01&to=2026-05-18
    /// </summary>
    [HttpGet("haccp-report")]
    public async Task<IActionResult> HaccpReport(DateOnly? from, DateOnly? to)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.Today);

        var report = await temperatureService.GetHaccpReportAsync(dateFrom, dateTo);
        return View(report);
    }
}
