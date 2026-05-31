using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Packing,PackingManager,Admin")]
[Route("packing")]
public sealed class PackingController : Controller
{
    private readonly IPackingService packingService;
    private readonly ILoadingService loadingService;
    private readonly IPackingIncidentService packingIncidentService;

    public PackingController(
        IPackingService packingService,
        ILoadingService loadingService,
        IPackingIncidentService packingIncidentService)
    {
        this.packingService = packingService;
        this.loadingService = loadingService;
        this.packingIncidentService = packingIncidentService;
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
    public IActionResult Loading(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return RedirectToAction("Index", "Loading", new { date = selectedDate });
    }

    [HttpGet("loading/{routeId:int}")]
    public IActionResult Delivery(int routeId, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return RedirectToAction("Route", "Loading", new { routeId, date = selectedDate });
    }

    [HttpPost("start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(DateOnly date, string packedBy)
    {
        await packingService.StartPackingSessionAsync(date, packedBy);
        TempData["Success"] = "Lista toreb do kompletacji odświeżona.";
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
            TempData["Success"] = $"Zamówienie #{orderId} spakowane.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("loading/{routeId:int}/manifest")]
    [ValidateAntiForgeryToken]
    public IActionResult GenerateManifest(int routeId, DateOnly date)
    {
        return RedirectToAction("Route", "Loading", new { routeId, date });
    }

    [HttpPost("loading/{routeId:int}/manifest/verify")]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyManifest(int routeId, DateOnly date)
    {
        return RedirectToAction("Route", "Loading", new { routeId, date });
    }

    [HttpGet("loading/{routeId:int}/manifest")]
    public IActionResult ManifestJson(int routeId, DateOnly date)
    {
        return RedirectToAction("ManifestJson", "Loading", new { routeId, date });
    }

    [HttpPost("session/{sessionId:int}/prepare-boxes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareBoxes(int sessionId)
    {
        try
        {
            var boxes = await packingService.PrepareOrderBoxesAsync(sessionId);
            TempData["Success"] = $"Przygotowano {boxes.Count()} pudełek testowych.";
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
            TempData["Success"] = "Pudełko oznaczone jako spakowane.";
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
            var result = await packingService.ScanBoxAsync(sessionId, barcode, packedBy);
            TempData["ScanCode"] = barcode.Trim();
            TempData["ScanMeal"] = result.MealName;
            TempData["ScanStatus"] = result.Status;
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Session), new { sessionId });
            }

            TempData["Success"] = result.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpGet("session/{sessionId:int}/items/{packingItemId:int}/issue")]
    public async Task<IActionResult> ItemIssue(int sessionId, int packingItemId)
    {
        try
        {
            return View(await BuildItemIssueViewModelAsync(sessionId, packingItemId));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Session), new { sessionId });
        }
    }

    [HttpPost("session/{sessionId:int}/items/{packingItemId:int}/issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ItemIssue(int sessionId, int packingItemId, PackingItemIssueFormViewModel model)
    {
        model.SessionId = sessionId;
        model.PackingItemId = packingItemId;
        ValidateIssueForm(model.ReasonFlags, model.Description);

        if (!ModelState.IsValid)
        {
            return View(await BuildItemIssueViewModelAsync(sessionId, packingItemId, model.ReasonFlags, model.Description));
        }

        try
        {
            await packingIncidentService.CreateItemIssueAsync(new CreatePackingItemIssueRequest
            {
                PackingItemId = packingItemId,
                ReasonFlags = model.ReasonFlags,
                Description = model.Description,
            });
            TempData["Success"] = "Zgłoszenie pudełka zapisane. Utworzono zamiennik i zadanie dla kuchni.";
            return RedirectToAction(nameof(Session), new { sessionId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildItemIssueViewModelAsync(sessionId, packingItemId, model.ReasonFlags, model.Description));
        }
    }

    [HttpGet("session/{sessionId:int}/bags/{packingBagId:int}/issue")]
    public async Task<IActionResult> BagIssue(int sessionId, int packingBagId)
    {
        try
        {
            return View(await BuildBagIssueViewModelAsync(sessionId, packingBagId));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Session), new { sessionId });
        }
    }

    [HttpPost("session/{sessionId:int}/bags/{packingBagId:int}/issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BagIssue(int sessionId, int packingBagId, PackingBagIssueFormViewModel model)
    {
        model.SessionId = sessionId;
        model.PackingBagId = packingBagId;
        ValidateIssueForm(model.ReasonFlags, model.Description);

        if (!ModelState.IsValid)
        {
            return View(await BuildBagIssueViewModelAsync(sessionId, packingBagId, model.ReasonFlags, model.Description));
        }

        try
        {
            await packingIncidentService.CreateBagIssueAsync(new CreatePackingBagIssueRequest
            {
                PackingBagId = packingBagId,
                ReasonFlags = model.ReasonFlags,
                Description = model.Description,
                AllowVerifiedManifestChange = User.IsInRole("PackingManager") || User.IsInRole("Admin"),
            });
            TempData["Success"] = "Zgłoszenie torby zapisane. Utworzono torbę zastępczą i sprawę dla admina.";
            return RedirectToAction(nameof(Session), new { sessionId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildBagIssueViewModelAsync(sessionId, packingBagId, model.ReasonFlags, model.Description));
        }
    }

    [HttpPost("box/{packingItemId:int}/issue")]
    [ValidateAntiForgeryToken]
    public IActionResult ReportBoxIssue(int packingItemId, int sessionId, string issueType, string reason)
    {
        return RedirectToAction(nameof(ItemIssue), new { sessionId, packingItemId });
    }

    [HttpPost("bag/{packingBagId:int}/damage")]
    [ValidateAntiForgeryToken]
    public IActionResult ReportBagDamage(int packingBagId, int sessionId, string reason)
    {
        return RedirectToAction(nameof(BagIssue), new { sessionId, packingBagId });
    }

    [HttpPost("session/{sessionId:int}/pack-bag")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PackBag(int sessionId, string packedBy)
    {
        try
        {
            await packingService.PackOrderBagAsync(sessionId, packedBy);
            TempData["Success"] = "Torba zamknięta.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Session), new { sessionId });
    }

    [HttpPost("loading/{routeId:int}/bag/{sessionId:int}/load")]
    [ValidateAntiForgeryToken]
    public IActionResult LoadBag(int routeId, int sessionId, DateOnly date)
    {
        return RedirectToAction("Route", "Loading", new { routeId, date });
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
            var bag = await loadingService.LoadBagByCodeAsync(routeId, transportCode);
            TempData["Success"] = $"Zeskanowano i załadowano torbę: {transportCode} (Zamówienie #{bag.OrderId}, Klient ID: {bag.ClientPublicId ?? "-"})";
            return RedirectToAction(nameof(Delivery), new { routeId, date });
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

    [HttpGet("labels/{sessionId:int}/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    public async Task<IActionResult> ReprintLabelForm(int sessionId)
    {
        try
        {
            var labels = (await packingService.GenerateTransportLabelsAsync(sessionId)).ToList();
            var model = new TransportLabelReprintFormViewModel
            {
                SessionId = sessionId,
                Labels = labels,
            };

            return View("ReprintLabel", model);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Labels), new { sessionId });
        }
    }

    [HttpPost("labels/{sessionId:int}/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprintLabel(int sessionId, string reprintReason)
    {
        if (string.IsNullOrWhiteSpace(reprintReason))
        {
            TempData["Error"] = "Podaj powód redruku etykiety transportowej.";
            return RedirectToAction(nameof(ReprintLabelForm), new { sessionId });
        }

        try
        {
            var labels = await packingService.GenerateTransportLabelsAsync(
                sessionId,
                reprintReason,
                forceNewPrint: true);
            TempData["Success"] = $"Wygenerowano redruk etykiety transportowej #{labels.First().Id}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Nie udało się wygenerować redruku etykiety transportowej: {ex.Message}";
        }

        return RedirectToAction(nameof(Labels), new { sessionId });
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
            await loadingService.DispatchAsync(date, routeId);
            TempData["Success"] = "Cała dostawa zatwierdzona do wysyłki.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Delivery), new { routeId, date });
    }

    private async Task<PackingItemIssueFormViewModel> BuildItemIssueViewModelAsync(
        int sessionId,
        int packingItemId,
        List<PackingIncidentReasonFlag>? selectedFlags = null,
        string? description = null)
    {
        var session = await packingService.GetSessionByIdAsync(sessionId)
            ?? throw new InvalidOperationException($"Sesja kompletacji #{sessionId} nie istnieje.");
        var item = session.Items.FirstOrDefault(i => i.Id == packingItemId)
            ?? throw new InvalidOperationException($"Pudełko #{packingItemId} nie należy do tej sesji.");

        return new PackingItemIssueFormViewModel
        {
            SessionId = sessionId,
            PackingItemId = packingItemId,
            ClientPublicId = session.ClientPublicIdDisplay,
            DeliveryCalendarId = session.DeliveryCalendarId,
            BoxCode = item.BoxCode,
            MealName = item.MealName,
            DietVariantId = item.DietVariantId,
            Status = item.Status,
            ReasonFlags = selectedFlags ?? new List<PackingIncidentReasonFlag>(),
            Description = description,
            ReasonOptions = ItemIssueReasonOptions,
        };
    }

    private async Task<PackingBagIssueFormViewModel> BuildBagIssueViewModelAsync(
        int sessionId,
        int packingBagId,
        List<PackingIncidentReasonFlag>? selectedFlags = null,
        string? description = null)
    {
        var session = await packingService.GetSessionByIdAsync(sessionId)
            ?? throw new InvalidOperationException($"Sesja kompletacji #{sessionId} nie istnieje.");
        var board = await packingService.GetPackingBoardAsync(session.PackingDate);
        var bag = board.Routes
            .SelectMany(r => r.Bags)
            .FirstOrDefault(b => b.PackingBagId == packingBagId)
            ?? throw new InvalidOperationException($"Torba #{packingBagId} nie należy do tej sesji.");

        return new PackingBagIssueFormViewModel
        {
            SessionId = sessionId,
            PackingBagId = packingBagId,
            ClientPublicId = session.ClientPublicIdDisplay,
            DeliveryCalendarId = session.DeliveryCalendarId,
            BagCode = bag.BagCode,
            Status = bag.StatusText,
            TotalBoxes = bag.TotalBoxes,
            ReasonFlags = selectedFlags ?? new List<PackingIncidentReasonFlag>(),
            Description = description,
            ReasonOptions = BagIssueReasonOptions,
        };
    }

    private void ValidateIssueForm(IReadOnlyCollection<PackingIncidentReasonFlag> reasonFlags, string? description)
    {
        if (reasonFlags.Count == 0)
        {
            ModelState.AddModelError(nameof(PackingItemIssueFormViewModel.ReasonFlags), "Wybierz co najmniej jeden powód zgłoszenia.");
        }

        if (reasonFlags.Contains(PackingIncidentReasonFlag.Other) && string.IsNullOrWhiteSpace(description))
        {
            ModelState.AddModelError(nameof(PackingItemIssueFormViewModel.Description), "Opis jest wymagany dla powodu „Inne”.");
        }
    }

    private static readonly IReadOnlyList<PackingIssueReasonOption> ItemIssueReasonOptions =
    [
        new() { Flag = PackingIncidentReasonFlag.BoxDamaged, Label = "Pudełko uszkodzone" },
        new() { Flag = PackingIncidentReasonFlag.BoxLeaking, Label = "Pudełko przecieka" },
        new() { Flag = PackingIncidentReasonFlag.BoxMissing, Label = "Brak pudełka" },
        new() { Flag = PackingIncidentReasonFlag.LabelUnreadable, Label = "Nieczytelna etykieta" },
        new() { Flag = PackingIncidentReasonFlag.WrongMeal, Label = "Nieprawidłowe danie" },
        new() { Flag = PackingIncidentReasonFlag.Other, Label = "Inne" },
    ];

    private static readonly IReadOnlyList<PackingIssueReasonOption> BagIssueReasonOptions =
    [
        new() { Flag = PackingIncidentReasonFlag.BagTorn, Label = "Torba rozerwana" },
        new() { Flag = PackingIncidentReasonFlag.BagDirty, Label = "Torba zabrudzona" },
        new() { Flag = PackingIncidentReasonFlag.BagClosureDamaged, Label = "Uszkodzone zamknięcie/uchwyt" },
        new() { Flag = PackingIncidentReasonFlag.TransportLabelDamaged, Label = "Etykieta transportowa uszkodzona" },
        new() { Flag = PackingIncidentReasonFlag.BagMissing, Label = "Brak torby" },
        new() { Flag = PackingIncidentReasonFlag.Other, Label = "Inne" },
    ];
}
