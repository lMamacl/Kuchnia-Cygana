using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

/// <summary>
/// Kontroler kompletacji: osobno pakowanie toreb oraz zaladunek dostaw do aut.
/// </summary>
[Authorize(Roles = "Packing,PackingManager,Admin")]
[Route("packing")]
public sealed class PackingController : Controller
{
    private readonly IPackingService packingService;

    public PackingController(IPackingService packingService)
    {
        this.packingService = packingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await packingService.GetPackingBoardAsync(selectedDate);
        ViewBag.SelectedDate = selectedDate;
        return View(board);
    }

    [HttpGet("loading")]
    public async Task<IActionResult> Loading(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await packingService.GetPackingBoardAsync(selectedDate);
        ViewBag.SelectedDate = selectedDate;
        return View(board);
    }

    [HttpGet("loading/{routeId:int}")]
    public async Task<IActionResult> Delivery(int routeId, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await packingService.GetPackingBoardAsync(selectedDate);
        var route = board.Routes.FirstOrDefault(r => r.RouteId == routeId);

        if (route is null)
        {
            TempData["Error"] = $"Dostawa/trasa #{routeId} nie istnieje.";
            return RedirectToAction(nameof(Loading), new { date = selectedDate });
        }

        return View(new PackingDeliveryViewModel
        {
            SelectedDate = selectedDate,
            Route = route,
            Manifest = await packingService.GetLatestPackingManifestAsync(selectedDate, routeId),
        });
    }

    [HttpPost("start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(DateOnly date, string packedBy)
    {
        await packingService.StartPackingSessionAsync(date, packedBy);
        TempData["Success"] = "Lista toreb do kompletacji odswiezona.";
        return RedirectToAction(nameof(Index), new { date });
    }

    [HttpGet("session")]
    [HttpGet("session/{sessionId:int}")]
    public async Task<IActionResult> Session(int sessionId = 0)
    {
        if (sessionId <= 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var session = await packingService.GetSessionByIdAsync(sessionId);
        if (session == null)
        {
            TempData["Error"] = $"Torba #{sessionId} nie istnieje.";
            return RedirectToAction(nameof(Index));
        }

        var board = await packingService.GetPackingBoardAsync(session.PackingDate);
        var bag = board.Routes
            .SelectMany(r => r.Bags)
            .FirstOrDefault(b => b.PackingSessionId == sessionId);

        return View(new PackingSessionViewModel
        {
            Session = session,
            Bag = bag,
            SelectedDate = session.PackingDate,
        });
    }

    [HttpPost("pack-client")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PackClient(int sessionId, int orderId)
    {
        try
        {
            await packingService.PackClientDietAsync(sessionId, orderId);
            TempData["Success"] = $"Zamowienie #{orderId} spakowane.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("loading/{routeId:int}/manifest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateManifest(int routeId, DateOnly date)
    {
        try
        {
            var generatedBy = User.Identity?.Name ?? "Packing";
            var manifest = await packingService.GeneratePackingManifestAsync(date, routeId, generatedBy);
            TempData["Success"] = $"Zapisano manifest dostawy {manifest.ManifestNumber}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Delivery), new { routeId, date });
    }

    [HttpPost("loading/{routeId:int}/manifest/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyManifest(int routeId, DateOnly date)
    {
        try
        {
            var verifiedBy = User.Identity?.Name ?? "Packing";
            var manifest = await packingService.VerifyPackingManifestAsync(date, routeId, verifiedBy);
            TempData["Success"] = $"Manifest {manifest.ManifestNumber} zweryfikowany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Delivery), new { routeId, date });
    }

    [HttpGet("loading/{routeId:int}/manifest")]
    public async Task<IActionResult> ManifestJson(int routeId, DateOnly date)
    {
        var manifest = await packingService.GetLatestPackingManifestAsync(date, routeId);
        if (manifest is null)
        {
            TempData["Error"] = $"Brak zapisanego manifestu dla trasy #{routeId} z dnia {date:dd.MM.yyyy}.";
            return RedirectToAction(nameof(Delivery), new { routeId, date });
        }

        return Content(manifest.PayloadJson, "application/json");
    }

    [HttpPost("session/{sessionId:int}/prepare-boxes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareBoxes(int sessionId)
    {
        try
        {
            var boxes = await packingService.PrepareOrderBoxesAsync(sessionId);
            TempData["Success"] = $"Przygotowano {boxes.Count()} pudelek.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("box/{packingItemId:int}/pack")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkBoxPacked(int packingItemId, int sessionId, string packedBy)
    {
        try
        {
            await packingService.MarkBoxPackedAsync(packingItemId, packedBy);
            TempData["Success"] = "Pudelko oznaczone jako spakowane.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("session/{sessionId:int}/scan-box")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanBox(int sessionId, string barcode)
    {
        var packedBy = User.Identity?.Name ?? "Packing";
        if (string.IsNullOrWhiteSpace(barcode))
        {
            TempData["Error"] = "Kod kreskowy/QR nie może być pusty.";
            return RedirectToAction(nameof(Session), new { sessionId });
        }

        try
        {
            await packingService.PackBoxByCodeAsync(sessionId, barcode, packedBy);
            TempData["Success"] = $"Zeskanowano i spakowano pudełko: {barcode}";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("session/{sessionId:int}/pack-bag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PackBag(int sessionId, string packedBy)
    {
        try
        {
            await packingService.PackOrderBagAsync(sessionId, packedBy);
            TempData["Success"] = "Torba oznaczona jako spakowana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("loading/{routeId:int}/bag/{sessionId:int}/load")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadBag(int routeId, int sessionId, DateOnly date)
    {
        try
        {
            await packingService.LoadOrderBagAsync(sessionId);
            TempData["Success"] = "Torba zaladowana do auta.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Delivery), new { routeId, date });
    }

    [HttpPost("loading/{routeId:int}/scan-bag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanBag(int routeId, string transportCode, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(transportCode))
        {
            TempData["Error"] = "Kod etykiety transportowej nie może być pusty.";
            return RedirectToAction(nameof(Delivery), new { routeId, date });
        }

        try
        {
            var bag = await packingService.LoadBagByCodeAsync(routeId, transportCode);
            TempData["Success"] = $"Zeskanowano i załadowano torbę: {transportCode} (Zamówienie #{bag.OrderId}, Klient: {bag.ClientName})";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Delivery), new { routeId, date });
    }

    [HttpGet("labels")]
    public IActionResult LabelsIndex()
    {
        return View();
    }

    [HttpGet("labels/{sessionId:int}")]
    public async Task<IActionResult> Labels(int sessionId)
    {
        var labels = await packingService.GenerateTransportLabelsAsync(sessionId);
        ViewBag.SessionId = sessionId;
        return View(labels);
    }

    [HttpGet("loading/{routeId:int}/labels")]
    public async Task<IActionResult> DeliveryLabels(int routeId, DateOnly date)
    {
        var labels = await packingService.GetTransportLabelsForDeliveryAsync(date, routeId);
        ViewBag.RouteId = routeId;
        ViewBag.SelectedDate = date;
        return View("Labels", labels);
    }

    [HttpPost("loading/{routeId:int}/dispatch")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DispatchDelivery(int routeId, DateOnly date)
    {
        try
        {
            await packingService.DispatchDeliveryAsync(date, routeId);
            TempData["Success"] = "Cala dostawa zatwierdzona do wysylki.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Delivery), new { routeId, date });
    }
}
