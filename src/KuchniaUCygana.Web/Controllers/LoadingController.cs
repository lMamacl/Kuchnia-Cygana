using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Packing,PackingManager,Admin")]
[Route("loading")]
public sealed class LoadingController : Controller
{
    private readonly IPackingService _packingService;
    private readonly ILoadingService _loadingService;
    private readonly IManifestService _manifestService;

    public LoadingController(
        IPackingService packingService,
        ILoadingService loadingService,
        IManifestService manifestService)
    {
        _packingService = packingService;
        _loadingService = loadingService;
        _manifestService = manifestService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await _packingService.GetPackingBoardAsync(selectedDate);
        ViewBag.SelectedDate = selectedDate;
        return View(board);
    }

    [HttpGet("{routeId:int}")]
    public async Task<IActionResult> Route(int routeId, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var board = await _packingService.GetPackingBoardAsync(selectedDate);
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
            Manifest = await _manifestService.GetManifestAsync(selectedDate, routeId),
        });
    }

    [HttpGet("{routeId:int}/manifest")]
    public async Task<IActionResult> Manifest(int routeId, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        try
        {
            var manifestControl = await _manifestService.GetManifestControlAsync(selectedDate, routeId);
            return View(new PackingDeliveryViewModel
            {
                SelectedDate = selectedDate,
                Route = manifestControl.Route,
                Manifest = manifestControl.Manifest,
                ManifestControl = manifestControl,
            });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { date = selectedDate });
        }
    }

    [HttpPost("{routeId:int}/manifest")]
    [HttpPost("{routeId:int}/manifest/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateManifest(int routeId, DateOnly date, string? changeReason)
    {
        try
        {
            var generatedBy = User.Identity?.Name ?? "Packing";
            var manifest = await _manifestService.GenerateManifestAsync(date, routeId, generatedBy, changeReason);
            TempData["Success"] = $"Zapisano manifest {manifest.ManifestNumber}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToManifest(routeId, date);
    }

    [HttpPost("{routeId:int}/manifest/worker-approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkerApproveManifest(int routeId, DateOnly date, bool confirmedManifest)
    {
        if (!confirmedManifest)
        {
            TempData["Error"] = "Przed zatwierdzeniem potwierdź kontrolę manifestu.";
            return RedirectToManifest(routeId, date);
        }

        try
        {
            var approvedBy = User.Identity?.Name ?? "Packing";
            await _manifestService.ApproveManifestByWorkerAsync(date, routeId, approvedBy);
            TempData["Success"] = "Manifest zatwierdzony przez pracownika. Przełożony otrzyma powiadomienie.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToManifest(routeId, date);
    }

    [HttpPost("{routeId:int}/manifest/supervisor-approve")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SupervisorApproveManifest(int routeId, DateOnly date, bool confirmedSupervisor)
    {
        if (!confirmedSupervisor)
        {
            TempData["Error"] = "Przed finalnym zatwierdzeniem potwierdź wysłanie manifestu do logistyki.";
            return RedirectToManifest(routeId, date);
        }

        try
        {
            var approvedBy = User.Identity?.Name ?? "PackingManager";
            await _manifestService.ApproveManifestBySupervisorAsync(date, routeId, approvedBy);
            TempData["Success"] = "Manifest finalnie zatwierdzony i wysłany do logistyki.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToManifest(routeId, date);
    }

    [HttpGet("{routeId:int}/manifest/preview")]
    public IActionResult ManifestPreview(int routeId, DateOnly date)
    {
        return RedirectToManifest(routeId, date);
    }

    [HttpPost("{routeId:int}/manifest/verify")]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyManifest(int routeId, DateOnly date, bool confirmedManifest)
    {
        return RedirectToManifest(routeId, date);
    }

    [HttpGet("{routeId:int}/manifest/payload")]
    public async Task<IActionResult> ManifestPayload(int routeId, DateOnly date)
    {
        var manifest = await _manifestService.GetManifestAsync(date, routeId);
        if (manifest is null)
        {
            TempData["Error"] = $"Brak zapisanego manifestu dla trasy #{routeId} z dnia {date:dd.MM.yyyy}.";
            return RedirectToManifest(routeId, date);
        }

        return Content(manifest.PayloadJson, "application/json");
    }

    [HttpPost("{routeId:int}/bag/{sessionId:int}/load")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadBag(int routeId, int sessionId, DateOnly date)
    {
        try
        {
            await _loadingService.LoadOrderBagAsync(sessionId);
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
            var bag = await _loadingService.LoadBagByCodeAsync(routeId, transportCode);
            TempData["Success"] = $"Zeskanowano i załadowano torbę: {transportCode} (zamówienie #{bag.OrderId}, klient ID: {bag.ClientPublicId ?? "-"})";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Route), new { routeId, date });
    }

    [HttpPost("reset")]
    [HttpPost("{routeId:int}/reset")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetLoading(DateOnly date, int? routeId)
    {
        try
        {
            var resetBags = await _loadingService.ResetLoadingAsync(date, routeId);
            TempData["Success"] = routeId.HasValue
                ? $"Reset załadunku trasy #{routeId.Value}: cofnięto {resetBags} toreb."
                : $"Reset załadunku dla {date:dd.MM.yyyy}: cofnięto {resetBags} toreb.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return routeId.HasValue
            ? RedirectToAction(nameof(Route), new { routeId = routeId.Value, date })
            : RedirectToAction(nameof(Index), new { date });
    }

    [HttpGet("{routeId:int}/labels")]
    public IActionResult DeliveryLabels(int routeId, DateOnly date)
    {
        return RedirectToAction("RouteLabels", "Packing", new { routeId, date });
    }

    [HttpGet("{routeId:int}/labels/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    public IActionResult ReprintDeliveryLabelsForm(int routeId, DateOnly date)
    {
        return RedirectToAction("LabelsIndex", "Packing", new { routeId, date });
    }

    [HttpPost("{routeId:int}/labels/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public IActionResult ReprintDeliveryLabels(int routeId, DateOnly date, string reprintReason)
    {
        return RedirectToAction("LabelsIndex", "Packing", new { routeId, date });
    }

    [HttpPost("{routeId:int}/dispatch")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DispatchDelivery(int routeId, DateOnly date)
    {
        try
        {
            await _loadingService.DispatchAsync(date, routeId);
            TempData["Success"] = "Załadunek zakończony i dostawa wysłana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Route), new { routeId, date });
    }

    private IActionResult RedirectToManifest(int routeId, DateOnly date)
    {
        var url = Url.Action(nameof(Manifest), new { routeId, date = date.ToString("yyyy-MM-dd") })
            ?? $"/loading/{routeId}/manifest?date={date:yyyy-MM-dd}";

        return Redirect(url);
    }
}
