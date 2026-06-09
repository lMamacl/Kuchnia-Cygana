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
    private readonly IPackingIncidentService packingIncidentService;
    private readonly IPackingSynchronizationService packingSynchronizationService;
    private readonly IPackingBagService packingBagService;

    public PackingController(
        IPackingService packingService,
        IPackingIncidentService packingIncidentService,
        IPackingSynchronizationService packingSynchronizationService,
        IPackingBagService packingBagService)
    {
        this.packingService = packingService;
        this.packingIncidentService = packingIncidentService;
        this.packingSynchronizationService = packingSynchronizationService;
        this.packingBagService = packingBagService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateOnly? date,
        string? search,
        int? routeId,
        string? labelStatus,
        string? bagStatus,
        int page = 1,
        int pageSize = 20,
        string? mode = null)
    {
        if (string.Equals(mode, "labels", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(LabelsIndex), new
            {
                date,
                search,
                routeId,
                labelStatus,
                bagStatus,
                page,
                pageSize,
            });
        }

        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var boardPage = await packingService.GetPackingBoardPageAsync(new PackingBoardQueryDto
        {
            Date = selectedDate,
            Search = search,
            RouteId = routeId,
            LabelStatus = "all",
            BagStatus = bagStatus,
            Page = page,
            PageSize = pageSize,
            Mode = "packing",
        });

        return View(BuildPackingIndexViewModel(
            boardPage,
            selectedDate,
            search,
            routeId,
            "all",
            bagStatus,
            "packing"));
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
        var result = await packingSynchronizationService.EnsureSessionsForDateAsync(date, packedBy);
        TempData["Success"] = $"Synchronizacja kompletacji zakończona. Nowe sesje: {result.CreatedSessions}, nowe torby: {result.CreatedBags}, aktualizacje: {result.UpdatedSessions}.";
        return RedirectToAction(nameof(Index), new { date });
    }

    [HttpPost("refresh-logistics")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshFromLogistics(DateOnly date)
    {
        try
        {
            var result = await packingSynchronizationService.RefreshFromRoutesAsync(
                date,
                User.Identity?.Name ?? "Packing");
            TempData["Success"] =
                $"Odświeżono kompletację z logistyki: trasy {result.RefreshedRoutes}, sesje +{result.CreatedSessions}, torby +{result.CreatedBags}, pudełka +{result.CreatedItems}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { date });
    }

    /// <summary>
    /// Displays the active packing session view, optionally for a specified session ID.
    /// </summary>
    /// <param name="sessionId">The session identifier to preview; when omitted the route uses a default of 0.</param>
    /// <returns>The view for the active packing session.</returns>
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

        var physicalBag = (await packingBagService.GetBagsForSessionAsync(sessionId))
            .OrderBy(b => b.BagNumber)
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        var activeBoxes = session.Items
            .Where(item => item.Status is not "Damaged" and not "Missing")
            .ToList();
        var bag = physicalBag is null
            ? null
            : new PackingBagDto
            {
                PackingBagId = physicalBag.Id,
                PackingSessionId = session.Id,
                DeliveryCalendarId = session.DeliveryCalendarId,
                BagNumber = physicalBag.BagNumber,
                BagCode = physicalBag.BagCode,
                OrderId = session.OrderId ?? 0,
                ClientPublicId = session.ClientPublicIdDisplay,
                Status = physicalBag.Status.ToString(),
                StatusText = TranslateBagStatus(physicalBag.Status),
                StatusColor = GetBagStatusColor(physicalBag.Status),
                TotalBoxes = activeBoxes.Count,
                PackedBoxes = activeBoxes.Count(item => item.Status == "Packed"),
            };

        return View(new PackingSessionViewModel
        {
            Session = session,
            Bag = bag,
            SelectedDate = session.PackingDate,
        });
    }

    [HttpPost("loading/{routeId:int}/manifest")]
    [ValidateAntiForgeryToken]
    public IActionResult GenerateManifest(int routeId, DateOnly date)
    {
        return RedirectToLoadingManifest(routeId, date);
    }

    [HttpPost("loading/{routeId:int}/manifest/verify")]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyManifest(int routeId, DateOnly date)
    {
        return RedirectToLoadingManifest(routeId, date);
    }

    [HttpGet("loading/{routeId:int}/manifest")]
    public IActionResult ManifestJson(int routeId, DateOnly date)
    {
        return RedirectToLoadingManifest(routeId, date);
    }

    [HttpPost("session/{sessionId:int}/prepare-boxes")]
    [Authorize(Roles = "Packing,PackingManager,Admin")]
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
    [Authorize(Roles = "Packing,PackingManager,Admin")]
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
        TempData["Info"] = "Załadunek obsługuje nowy ekran /loading. Akcja legacy została przekierowana bez zmiany danych.";
        return RedirectToLoadingRoute(routeId, date);
    }

    [HttpPost("loading/{routeId:int}/scan-bag")]
    [ValidateAntiForgeryToken]
    public IActionResult ScanBag(int routeId, string transportCode, DateOnly date)
    {
        TempData["Info"] = "Skanowanie załadunku obsługuje nowy ekran /loading. Akcja legacy została przekierowana bez zmiany danych.";
        return RedirectToLoadingRoute(routeId, date);
    }

    [HttpGet("label")]
    public IActionResult LabelAlias(
        DateOnly? date,
        string? search,
        int? routeId,
        string? labelStatus,
        string? bagStatus,
        int page = 1,
        int pageSize = 20)
    {
        return RedirectToAction(nameof(LabelsIndex), new { date, search, routeId, labelStatus, bagStatus, page, pageSize });
    }

    /// <summary>
    /// Displays the labels preview view for the packing section.
    /// </summary>
    /// <returns>A view showing labels and QR codes in preview mode.</returns>
    [HttpGet("labels")]
    public async Task<IActionResult> LabelsIndex(
        DateOnly? date,
        string? search,
        int? routeId,
        string? labelStatus,
        string? bagStatus,
        int page = 1,
        int pageSize = 20)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var boardPage = await packingService.GetPackingBoardPageAsync(new PackingBoardQueryDto
        {
            Date = selectedDate,
            Search = search,
            RouteId = routeId,
            LabelStatus = labelStatus,
            BagStatus = bagStatus,
            Page = page,
            PageSize = pageSize,
            Mode = "labels",
        });

        return View("LabelsIndex", BuildPackingIndexViewModel(
            boardPage,
            selectedDate,
            search,
            routeId,
            labelStatus,
            bagStatus,
            "labels"));
    }

    /// <summary>
    /// Displays the labels view for the specified packing session in preview mode.
    /// </summary>
    /// <param name="sessionId">Identifier of the packing session whose labels are shown.</param>
    /// <returns>A view populated with preview metadata and a QR label placeholder for the given session.</returns>
    [HttpGet("labels/{sessionId:int}")]
    public async Task<IActionResult> Labels(int sessionId)
    {
        var labels = (await packingService.GetTransportLabelsForSessionAsync(sessionId)).ToList();
        if (labels.Count == 0)
        {
            TempData["Error"] = "Brak wygenerowanej etykiety transportowej dla tej torby. Użyj akcji Drukuj w widoku etykiet.";
            return RedirectToAction(nameof(Session), new { sessionId });
        }

        ViewBag.SessionId = sessionId;
        ViewBag.AutoPrint = true;
        return View(labels);
    }

    [HttpPost("labels/{sessionId:int}/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateLabel(int sessionId, DateOnly? date, string? returnUrl = null)
    {
        try
        {
            await packingService.GenerateTransportLabelsAsync(sessionId);
            TempData["Success"] = "Wygenerowano etykietę transportową.";
            return RedirectToAction(nameof(Labels), new { sessionId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl, date));
    }

    [HttpPost("labels/bags/{packingBagId:int}/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateBagLabel(int packingBagId, DateOnly? date, string? returnUrl = null)
    {
        try
        {
            var labels = (await packingService.GenerateTransportLabelsForBagAsync(packingBagId)).ToList();
            TempData["Success"] = "Wygenerowano etykietę transportową.";
            return labels.Count > 0
                ? PrepareLabelsPrintView(labels, autoPrint: true)
                : LocalRedirect(SafeReturnUrl(returnUrl, date));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl, date));
    }

    [HttpGet("labels/routes/{routeId:int}")]
    public async Task<IActionResult> RouteLabels(int routeId, DateOnly date, bool autoPrint = true)
    {
        try
        {
            var labels = (await packingService.GetTransportLabelsForRouteAsync(date, routeId)).ToList();
            if (labels.Count == 0)
            {
                TempData["Error"] = "Brak wygenerowanych etykiet transportowych dla tej trasy. Najpierw wygeneruj brakujące etykiety.";
                return RedirectToAction(nameof(LabelsIndex), new { date, routeId });
            }

            return PrepareLabelsPrintView(labels, date, routeId, autoPrint);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(LabelsIndex), new { date, routeId });
        }
    }

    [HttpPost("labels/routes/{routeId:int}/generate-missing")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateMissingRouteLabels(int routeId, DateOnly date, string? returnUrl = null)
    {
        try
        {
            var labels = (await packingService.GenerateMissingTransportLabelsForRouteAsync(date, routeId)).ToList();
            TempData["Success"] = $"Etykiety transportowe gotowe dla trasy: {labels.Count}.";
            return labels.Count > 0
                ? PrepareLabelsPrintView(labels, date, routeId, autoPrint: true)
                : LocalRedirect(SafeReturnUrl(returnUrl, date));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl, date));
    }

    [HttpPost("labels/selected/print")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintSelectedLabels(
        DateOnly date,
        int? routeId,
        string? selectedLabelIds,
        string? selectedBagIds,
        List<int> labelIds,
        List<int> packingBagIds,
        string? returnUrl = null)
    {
        var ids = ParseLabelIds(selectedLabelIds, labelIds);
        var bagIds = ParseLabelIds(selectedBagIds, packingBagIds);
        if (ids.Count == 0 && bagIds.Count == 0)
        {
            TempData["Error"] = "Zaznacz co najmniej jedna torbe do wygenerowania albo wydruku etykiety.";
            return LocalRedirect(SafeReturnUrl(returnUrl, date));
        }

        try
        {
            var labels = new List<PackingLabelDto>();
            foreach (var bagId in bagIds)
            {
                labels.AddRange(await packingService.GenerateTransportLabelsForBagAsync(bagId));
            }

            labels.AddRange(await GetSelectedTransportLabelsAsync(date, routeId, ids));
            labels = labels
                .GroupBy(label => label.Id)
                .Select(group => group.First())
                .OrderBy(label => label.StopNumber ?? int.MaxValue)
                .ThenBy(label => label.BagCode)
                .ToList();

            if (labels.Count == 0)
            {
                TempData["Error"] = "Nie znaleziono wygenerowanych etykiet dla zaznaczonych pozycji.";
                return LocalRedirect(SafeReturnUrl(returnUrl, date));
            }

            return PrepareLabelsPrintView(labels, date, routeId, autoPrint: true);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return LocalRedirect(SafeReturnUrl(returnUrl, date));
        }
    }

    [HttpPost("labels/{labelId:int}/attached")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmLabelAttached(int labelId, DateOnly? date, string? returnUrl = null)
    {
        try
        {
            await packingService.ConfirmTransportLabelAttachedAsync(labelId);
            TempData["Success"] = "Potwierdzono przyklejenie etykiety transportowej.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl, date));
    }

    [HttpPost("labels/attached")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmSelectedLabelsAttached(
        DateOnly? date,
        string? selectedLabelIds,
        List<int> labelIds,
        string? returnUrl = null)
    {
        var ids = ParseLabelIds(selectedLabelIds, labelIds);
        if (ids.Count == 0)
        {
            TempData["Error"] = "Zaznacz co najmniej jedna etykiete do potwierdzenia.";
            return LocalRedirect(SafeReturnUrl(returnUrl, date));
        }

        try
        {
            var count = await packingService.ConfirmTransportLabelsAttachedAsync(ids);
            TempData["Success"] = $"Potwierdzono przyklejenie etykiet: {count}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return LocalRedirect(SafeReturnUrl(returnUrl, date));
    }

    [HttpGet("labels/{sessionId:int}/reprint")]
    [Authorize(Roles = "PackingManager,Admin")]
    public async Task<IActionResult> ReprintLabelForm(int sessionId)
    {
        try
        {
            var labels = (await packingService.GetTransportLabelsForSessionAsync(sessionId)).ToList();
            if (labels.Count == 0)
            {
                TempData["Error"] = "Nie można redrukować etykiety, której jeszcze nie wygenerowano.";
                return RedirectToAction(nameof(LabelsIndex));
            }

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
    public IActionResult DeliveryLabels(int routeId, DateOnly date)
    {
        return RedirectToAction(nameof(RouteLabels), new { routeId, date });
    }

    [HttpPost("loading/{routeId:int}/dispatch")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public IActionResult DispatchDelivery(int routeId, DateOnly date)
    {
        TempData["Info"] = "Wysyłkę trasy obsługuje nowy ekran /loading. Akcja legacy została przekierowana bez zmiany danych.";
        return RedirectToLoadingRoute(routeId, date);
    }

    private static PackingIndexViewModel BuildPackingIndexViewModel(
        PackingBoardPageDto boardPage,
        DateOnly selectedDate,
        string? search,
        int? routeId,
        string? labelStatus,
        string? bagStatus,
        string? mode)
    {
        var safePageSize = Math.Clamp(boardPage.PageSize, 10, 100);
        var normalizedMode = string.Equals(mode, "labels", StringComparison.OrdinalIgnoreCase) ? "labels" : "packing";
        var normalizedLabelStatus = NormalizeFilterValue(labelStatus);
        var normalizedBagStatus = NormalizeFilterValue(bagStatus);
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var totalPages = boardPage.TotalBags == 0 ? 1 : (int)Math.Ceiling(boardPage.TotalBags / (double)safePageSize);
        var safePage = Math.Min(Math.Max(boardPage.Page, 1), totalPages);

        return new PackingIndexViewModel
        {
            SelectedDate = selectedDate,
            Board = boardPage.Board,
            AllRoutes = boardPage.AllRoutes,
            Mode = normalizedMode,
            Search = normalizedSearch,
            RouteId = routeId,
            LabelStatus = normalizedLabelStatus,
            BagStatus = normalizedBagStatus,
            Page = safePage,
            PageSize = safePageSize,
            TotalBags = boardPage.TotalBags,
        };
    }

    private static PackingRouteDto CreateRoutePage(PackingRouteDto source, List<PackingBagDto> bags)
    {
        return new PackingRouteDto
        {
            RouteId = source.RouteId,
            RouteName = source.RouteName,
            VehicleId = source.VehicleId,
            VehicleRegistration = source.VehicleRegistration,
            TotalBags = source.TotalBags,
            PackedBags = source.PackedBags,
            LoadedBags = source.LoadedBags,
            DispatchedBags = source.DispatchedBags,
            AllBagsPacked = source.AllBagsPacked,
            AllBagsLoaded = source.AllBagsLoaded,
            HasManifest = source.HasManifest,
            IsManifestVerified = source.IsManifestVerified,
            ManifestId = source.ManifestId,
            ManifestNumber = source.ManifestNumber,
            ManifestGeneratedAt = source.ManifestGeneratedAt,
            ManifestVerifiedAt = source.ManifestVerifiedAt,
            ManifestWorkerApprovedAt = source.ManifestWorkerApprovedAt,
            ManifestSentToLogisticsAt = source.ManifestSentToLogisticsAt,
            ManifestRequiresRegeneration = source.ManifestRequiresRegeneration,
            ManifestRequiresRegenerationReason = source.ManifestRequiresRegenerationReason,
            CanGenerateManifest = source.CanGenerateManifest,
            CanVerifyManifest = source.CanVerifyManifest,
            CanWorkerApproveManifest = source.CanWorkerApproveManifest,
            CanSupervisorApproveManifest = source.CanSupervisorApproveManifest,
            CanLoadBags = source.CanLoadBags,
            CanDispatchDelivery = source.CanDispatchDelivery,
            Bags = bags,
        };
    }

    private static bool MatchesSearch(PackingRouteBagRow row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(row.Bag.OrderId.ToString(), search) ||
            Contains(row.Bag.DeliveryCalendarId?.ToString(), search) ||
            Contains(row.Bag.BagCode, search) ||
            Contains(row.Bag.ClientPublicId, search) ||
            Contains(row.Route.RouteName, search) ||
            Contains(row.Route.VehicleRegistration, search);
    }

    private static bool MatchesBagStatus(PackingBagDto bag, string bagStatus)
    {
        return bagStatus == "all" || string.Equals(bag.Status, bagStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesLabelStatus(PackingBagDto bag, string labelStatus)
    {
        return labelStatus switch
        {
            "missing" => !bag.HasLabels,
            "generated" => bag.HasLabels,
            "attached" => bag.IsTransportLabelAttached,
            "not-attached" => bag.HasLabels && !bag.IsTransportLabelAttached,
            _ => true,
        };
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
    }

    private static string TranslateBagStatus(PackingBagStatus status)
    {
        return status switch
        {
            PackingBagStatus.Pending => "W toku",
            PackingBagStatus.Packed => "Spakowana",
            PackingBagStatus.Labeled => "Z etykietą",
            PackingBagStatus.Manifested => "W manifeście",
            PackingBagStatus.Loaded => "Załadowana",
            PackingBagStatus.Dispatched => "Wysłana",
            PackingBagStatus.Damaged => "Uszkodzona",
            _ => status.ToString(),
        };
    }

    private static string GetBagStatusColor(PackingBagStatus status)
    {
        return status switch
        {
            PackingBagStatus.Packed => "success",
            PackingBagStatus.Labeled => "azure",
            PackingBagStatus.Manifested => "indigo",
            PackingBagStatus.Loaded => "primary",
            PackingBagStatus.Dispatched => "dark",
            PackingBagStatus.Damaged => "danger",
            _ => "secondary",
        };
    }

    private string SafeReturnUrl(string? returnUrl, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action(nameof(LabelsIndex), new { date = selectedDate }) ?? "/";
    }

    private sealed record PackingRouteBagRow(PackingRouteDto Route, PackingBagDto Bag);

    private IActionResult PrepareLabelsPrintView(
        IReadOnlyCollection<PackingLabelDto> labels,
        DateOnly? selectedDate = null,
        int? routeId = null,
        bool autoPrint = false)
    {
        if (routeId.HasValue)
        {
            ViewBag.RouteId = routeId.Value;
        }
        else
        {
            var sessionIds = labels
                .Select(label => label.PackingSessionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            if (sessionIds.Count == 1)
            {
                ViewBag.SessionId = sessionIds[0];
            }
        }

        if (selectedDate.HasValue)
        {
            ViewBag.SelectedDate = selectedDate.Value;
        }

        ViewBag.AutoPrint = autoPrint;
        return View("Labels", labels);
    }

    private async Task<List<PackingLabelDto>> GetSelectedTransportLabelsAsync(
        DateOnly date,
        int? routeId,
        IReadOnlyCollection<int> labelIds)
    {
        var idSet = labelIds.Where(id => id > 0).ToHashSet();
        if (idSet.Count == 0)
        {
            return new List<PackingLabelDto>();
        }

        var labels = new List<PackingLabelDto>();
        if (routeId.HasValue)
        {
            labels.AddRange(await packingService.GetTransportLabelsForRouteAsync(date, routeId.Value));
        }
        else
        {
            var board = await packingService.GetPackingBoardAsync(date);
            var routeIds = board.Routes
                .Where(route => route.Bags.Any(bag => bag.TransportLabelId.HasValue && idSet.Contains(bag.TransportLabelId.Value)))
                .Select(route => route.RouteId)
                .Distinct()
                .ToList();

            foreach (var currentRouteId in routeIds)
            {
                labels.AddRange(await packingService.GetTransportLabelsForRouteAsync(date, currentRouteId));
            }
        }

        return labels
            .Where(label => idSet.Contains(label.Id))
            .OrderBy(label => label.StopNumber ?? int.MaxValue)
            .ThenBy(label => label.BagCode)
            .ToList();
    }

    private static List<int> ParseLabelIds(string? selectedLabelIds, IEnumerable<int>? labelIds)
    {
        var ids = new List<int>();
        if (!string.IsNullOrWhiteSpace(selectedLabelIds))
        {
            foreach (var value in selectedLabelIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(value, out var id))
                {
                    ids.Add(id);
                }
            }
        }

        if (labelIds is not null)
        {
            ids.AddRange(labelIds);
        }

        return ids.Where(id => id > 0).Distinct().ToList();
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
        var bag = (await packingBagService.GetBagsForSessionAsync(sessionId))
            .FirstOrDefault(b => b.Id == packingBagId)
            ?? throw new InvalidOperationException($"Torba #{packingBagId} nie należy do tej sesji.");
        var totalBoxes = session.Items.Count(item => item.Status is not "Damaged" and not "Missing");

        return new PackingBagIssueFormViewModel
        {
            SessionId = sessionId,
            PackingBagId = packingBagId,
            ClientPublicId = session.ClientPublicIdDisplay,
            DeliveryCalendarId = session.DeliveryCalendarId,
            BagCode = bag.BagCode,
            Status = bag.Status.ToString(),
            TotalBoxes = totalBoxes,
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

    private IActionResult RedirectToLoadingManifest(int routeId, DateOnly date)
    {
        var url = Url.Action("Manifest", "Loading", new { routeId, date = date.ToString("yyyy-MM-dd") })
            ?? $"/loading/{routeId}/manifest?date={date:yyyy-MM-dd}";

        return Redirect(url);
    }

    private IActionResult RedirectToLoadingRoute(int routeId, DateOnly date)
    {
        return RedirectToAction("Route", "Loading", new { routeId, date = date.ToString("yyyy-MM-dd") });
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
