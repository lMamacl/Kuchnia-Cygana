using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Packing,PackingManager,Admin")]
[Route("loading")]
public sealed class LoadingController : Controller
{
    private readonly IPackingService packingService;
    private readonly ILoadingService loadingService;

    public LoadingController(IPackingService packingService, ILoadingService loadingService)
    {
        this.packingService = packingService;
        this.loadingService = loadingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await packingService.GetPackingBoardAsync(selectedDate);
        ViewBag.SelectedDate = selectedDate;
        return View(board);
    }

    [HttpGet("{routeId:int}")]
    public async Task<IActionResult> Route(int routeId, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await packingService.GetPackingBoardAsync(selectedDate);
        var route = board.Routes.FirstOrDefault(r => r.RouteId == routeId);

        if (route is null)
        {
            TempData["Error"] = $"Dostawa/trasa #{routeId} nie istnieje.";
            return RedirectToAction(nameof(Index), new { date = selectedDate });
        }

        return View(new PackingDeliveryViewModel
        {
            SelectedDate = selectedDate,
            Route = route,
            Manifest = await loadingService.GetManifestAsync(selectedDate, routeId),
        });
    }

    [HttpPost("{routeId:int}/manifest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateManifest(int routeId, DateOnly date, string? changeReason)
    {
        try
        {
            var generatedBy = User.Identity?.Name ?? "Packing";
            var manifest = await loadingService.GenerateManifestAsync(date, routeId, generatedBy, changeReason);
            TempData["Success"] = $"Zapisano manifest dostawy {manifest.ManifestNumber}.";
            return RedirectToAction(nameof(ManifestPreview), new { routeId, date });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Route), new { routeId, date });
    }

    [HttpGet("{routeId:int}/manifest/preview")]
    public async Task<IActionResult> ManifestPreview(int routeId, DateOnly date)
    {
        var board = await packingService.GetPackingBoardAsync(date);
        var route = board.Routes.FirstOrDefault(r => r.RouteId == routeId);
        if (route is null)
        {
            TempData["Error"] = $"Dostawa/trasa #{routeId} nie istnieje.";
            return RedirectToAction(nameof(Index), new { date });
        }

        var manifest = await loadingService.GetManifestAsync(date, routeId);
        if (manifest is null)
        {
            TempData["Error"] = $"Brak zapisanego manifestu dla trasy #{routeId} z dnia {date:dd.MM.yyyy}.";
            return RedirectToAction(nameof(Route), new { routeId, date });
        }

        return View(new PackingDeliveryViewModel
        {
            SelectedDate = date,
            Route = route,
            Manifest = manifest,
        });
    }

    [HttpPost("{routeId:int}/manifest/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyManifest(int routeId, DateOnly date, bool confirmedManifest)
    {
        if (!confirmedManifest)
        {
            TempData["Error"] = "Przed zatwierdzeniem potwierdź kontrolę zgodności manifestu z torbami.";
            return RedirectToAction(nameof(ManifestPreview), new { routeId, date });
        }

        try
        {
            var verifiedBy = User.Identity?.Name ?? "Packing";
            var manifest = await loadingService.VerifyManifestAsync(date, routeId, verifiedBy);
            TempData["Success"] = $"Manifest {manifest.ManifestNumber} zweryfikowany.";
            return RedirectToAction(nameof(ManifestPreview), new { routeId, date });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(ManifestPreview), new { routeId, date });
    }

    [HttpGet("{routeId:int}/manifest")]
    public async Task<IActionResult> ManifestJson(int routeId, DateOnly date)
    {
        var manifest = await loadingService.GetManifestAsync(date, routeId);
        if (manifest is null)
        {
            TempData["Error"] = $"Brak zapisanego manifestu dla trasy #{routeId} z dnia {date:dd.MM.yyyy}.";
            return RedirectToAction(nameof(Route), new { routeId, date });
        }

        return Content(manifest.PayloadJson, "application/json");
    }

    [HttpPost("{routeId:int}/bag/{sessionId:int}/load")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadBag(int routeId, int sessionId, DateOnly date)
    {
        try
        {
            await loadingService.LoadOrderBagAsync(sessionId);
            TempData["Success"] = "Torba załadowana do auta.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Route), new { routeId, date });
    }

    [HttpPost("{routeId:int}/scan-bag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanBag(int routeId, string transportCode, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(transportCode))
        {
            TempData["Error"] = "Kod etykiety transportowej nie może być pusty.";
            return RedirectToAction(nameof(Route), new { routeId, date });
        }

        try
        {
            var bag = await loadingService.LoadBagByCodeAsync(routeId, transportCode);
            TempData["Success"] = $"Zeskanowano i załadowano torbę: {transportCode} (Zamówienie #{bag.OrderId}, Klient: {bag.ClientName})";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Route), new { routeId, date });
    }

    [HttpGet("{routeId:int}/labels")]
    public async Task<IActionResult> DeliveryLabels(int routeId, DateOnly date)
    {
        var labels = await packingService.GetTransportLabelsForDeliveryAsync(date, routeId);
        ViewBag.RouteId = routeId;
        ViewBag.SelectedDate = date;
        return View("Labels", labels);
    }

    [HttpGet("{routeId:int}/labels/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    public async Task<IActionResult> ReprintDeliveryLabelsForm(int routeId, DateOnly date)
    {
        try
        {
            var labels = (await packingService.GetTransportLabelsForDeliveryAsync(date, routeId)).ToList();
            var model = new TransportLabelReprintFormViewModel
            {
                RouteId = routeId,
                Date = date,
                Labels = labels,
            };

            return View("ReprintDeliveryLabels", model);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(DeliveryLabels), new { routeId, date });
        }
    }

    [HttpPost("{routeId:int}/labels/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprintDeliveryLabels(int routeId, DateOnly date, string reprintReason)
    {
        if (string.IsNullOrWhiteSpace(reprintReason))
        {
            TempData["Error"] = "Podaj powód redruku etykiet transportowych.";
            return RedirectToAction(nameof(ReprintDeliveryLabelsForm), new { routeId, date });
        }

        try
        {
            var labels = await packingService.GetTransportLabelsForDeliveryAsync(
                date,
                routeId,
                reprintReason,
                forceNewPrint: true);
            TempData["Success"] = $"Wygenerowano redruk {labels.Count()} etykiet transportowych.";
            return RedirectToAction(nameof(DeliveryLabels), new { routeId, date });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(DeliveryLabels), new { routeId, date });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Nie udało się wygenerować redruku etykiet transportowych: {ex.Message}";
            return RedirectToAction(nameof(ReprintDeliveryLabelsForm), new { routeId, date });
        }
    }

    [HttpPost("{routeId:int}/dispatch")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DispatchDelivery(int routeId, DateOnly date)
    {
        try
        {
            await loadingService.DispatchAsync(date, routeId);
            TempData["Success"] = "Cała dostawa zatwierdzona do wysyłki.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Route), new { routeId, date });
    }
}
