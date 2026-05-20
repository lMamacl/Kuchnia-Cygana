using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

/// <summary>
/// Kontroler kompletacji — sesje pakowania, pakowanie klientów, etykiety, wysyłka.
/// TASK-M3-026 | Stanowisko: Packing | Szef: PackingManager
/// </summary>
[AllowAnonymous]
[Route("packing")]
public sealed class PackingController : Controller
{
    private readonly IPackingService packingService;

    public PackingController(IPackingService packingService)
    {
        this.packingService = packingService;
    }

    // ── Sesje pakowania ──────────────────────────────────────

    /// <summary>
    /// Widok główny kompletacji — lista sesji pakowania na wybrany dzień.
    /// GET /packing
    /// </summary>
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
    {
        ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return View();
    }

    /// <summary>
    /// Rozpoczyna nową sesję pakowania.
    /// POST /packing/start
    /// </summary>
    [HttpPost("start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(DateOnly date, string packedBy)
    {
        var session = await packingService.StartPackingSessionAsync(date, packedBy);
        TempData["Success"] = $"Sesja #{session.Id} rozpoczęta przez {packedBy}.";
        return RedirectToAction(nameof(Session), new { sessionId = session.Id });
    }

    /// <summary>
    /// Widok szczegółowy sesji pakowania — lista spakowanych klientów.
    /// GET /packing/session/3
    /// </summary>
    [HttpGet("session")]
    [HttpGet("session/{sessionId:int}")]
    public IActionResult Session(int sessionId = 0)
    {
        ViewBag.SessionId = sessionId;
        return View();
    }

    // ── Pakowanie klienta ────────────────────────────────────

    /// <summary>
    /// Pakuje dietę klienta w ramach sesji (przypisuje posiłki z partii).
    /// POST /packing/pack-client
    /// </summary>
    [HttpPost("pack-client")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PackClient(int sessionId, int orderId)
    {
        var item = await packingService.PackClientDietAsync(sessionId, orderId);
        TempData["Success"] = $"Dieta klienta (zamówienie #{orderId}) spakowana.";
        return RedirectToAction(nameof(Session), new { sessionId });
    }

    // ── Etykiety ─────────────────────────────────────────────

    /// <summary>
    /// Generuje etykiety QR dla wszystkich pozycji w sesji.
    /// GET /packing/labels/3
    /// </summary>
    [HttpGet("labels")]
    public IActionResult LabelsIndex()
    {
        return View();
    }

    [HttpGet("labels/{sessionId:int}")]
    public IActionResult Labels(int sessionId)
    {
        ViewBag.SessionId = sessionId;
        return View();
    }

    // ── Zatwierdzanie wysyłki ────────────────────────────────

    /// <summary>
    /// Zatwierdza sesję jako gotową do wysyłki (zmiana statusu na Dispatched).
    /// POST /packing/dispatch/3
    /// </summary>
    [HttpPost("dispatch/{sessionId:int}")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(int sessionId)
    {
        await packingService.ApproveDispatchAsync(sessionId);
        TempData["Success"] = "Sesja zatwierdzona do wysyłki.";
        return RedirectToAction(nameof(Index));
    }
}
