using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

/// <summary>
/// Kontroler produkcji — plan dnia, karty gotowania, zatwierdzanie.
/// TASK-M3-024 | Stanowisko: Kitchen | Szef: KitchenManager
/// </summary>
[Authorize(Roles = "Kitchen,KitchenManager,Warehouse,WarehouseManager,Admin")]
[Route("production")]
public sealed class ProductionController : Controller
{
    private readonly IProductionService productionService;
    private readonly ICookingSessionService cookingSessionService;
    private readonly IWarehouseDemandService warehouseDemandService;
    private readonly IPackingService packingService;
    private readonly IPackingIncidentService packingIncidentService;
    private readonly IPackingSynchronizationService packingSynchronizationService;

    public ProductionController(
        IProductionService productionService,
        ICookingSessionService cookingSessionService,
        IWarehouseDemandService warehouseDemandService,
        IPackingService packingService,
        IPackingIncidentService packingIncidentService,
        IPackingSynchronizationService packingSynchronizationService)
    {
        this.productionService = productionService;
        this.cookingSessionService = cookingSessionService;
        this.warehouseDemandService = warehouseDemandService;
        this.packingService = packingService;
        this.packingIncidentService = packingIncidentService;
        this.packingSynchronizationService = packingSynchronizationService;
    }

    // ── Plan dnia ─────────────────────────────────────────────

    /// <summary>
    /// Widok główny produkcji — plan produkcji na wybrany dzień.
    /// GET /production
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] KitchenDashboardFilterDto filter)
    {
        if (filter.Date == default)
        {
            filter.Date = DateOnly.FromDateTime(DateTime.Today);
        }

        var dashboard = await productionService.GetKitchenDashboardAsync(filter);
        var m2PlanOverview = await productionService.GetM2PlanOverviewAsync(new ProductionM2PlanFilterDto
        {
            StartDate = filter.Date,
            Days = 7,
        });

        var viewModel = new ProductionDashboardViewModel
        {
            SelectedDate = dashboard.Filter.Date,
            DailyPlan = dashboard.Plan,
            Dashboard = dashboard,
            M2PlanOverview = m2PlanOverview,
        };

        return View(viewModel);
    }

    [HttpGet("generate")]
    public IActionResult Generate(DateOnly? date)
    {
        ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return View();
    }

    /// <summary>
    /// Generowanie planu produkcji na dany dzień.
    /// POST /production/generate
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Roles = "KitchenManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(CreateProductionPlanRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.SelectedDate = request.ProductionDate;
            return View(request);
        }

        try
        {
            var plan = await productionService.GenerateDailyPlanAsync(request);
        TempData["Success"] = $"Plan produkcji na {request.ProductionDate:dd.MM.yyyy} został wygenerowany ({plan.Items.Count} pozycji).";
            return RedirectToAction(nameof(Plan), new { planId = plan.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { date = request.ProductionDate });
        }
    }

    /// <summary>
    /// Widok szczegółowy planu produkcji.
    /// GET /production/plan/5
    /// </summary>
    [HttpGet("plan")]
    [HttpGet("plan/{planId:int}")]
    public async Task<IActionResult> Plan(int planId = 0, [FromQuery] KitchenDashboardFilterDto? filter = null)
    {
        if (planId == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var plan = await productionService.GetDailyPlanByDateAsync(today);
            if (plan != null)
            {
                return RedirectToAction(nameof(Plan), new { planId = plan.Id });
            }
            return RedirectToAction(nameof(Index));
        }

        var detail = await productionService.GetPlanDetailAsync(planId, filter ?? new KitchenDashboardFilterDto());
        if (detail == null)
        {
            TempData["Error"] = $"Nie znaleziono planu o ID #{planId}.";
            return RedirectToAction(nameof(Index));
        }

        return View(detail);
    }

    [HttpGet("m2-plan")]
    public async Task<IActionResult> M2Plan([FromQuery] ProductionM2PlanFilterDto filter)
    {
        if (filter.StartDate == default)
        {
            filter.StartDate = DateOnly.FromDateTime(DateTime.Today);
        }

        var overview = await productionService.GetM2PlanOverviewAsync(filter);
        return View(overview);
    }

    [HttpPost("m2-plan/alerts/{alertId:int}/ack")]
    [Authorize(Roles = "KitchenManager,WarehouseManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcknowledgeM2PlanAlert(int alertId, DateOnly startDate, int days = 7)
    {
        try
        {
            await productionService.AcknowledgePlanAlertAsync(alertId, GetOperatorName());
            TempData["Success"] = $"Potwierdzono odbior alertu M2 #{alertId}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(M2Plan), new { startDate, days });
    }

    [HttpPost("m2-plan/refresh-day")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshM2PlanDay(DateOnly date, DateOnly startDate, int days = 7)
    {
        try
        {
            var result = await productionService.RefreshProductionPlanFromM2Async(date, GetOperatorName());
            if (result.Status == "Blocked")
            {
                TempData["Error"] = $"Nie mozna odswiezyc planu na {date:dd.MM.yyyy}: "
                    + string.Join(" | ", result.Blockers.Take(3));
            }
            else
            {
                TempData["Success"] =
                    $"Odświeżono plan produkcji na {date:dd.MM.yyyy}: {result.Status}, pozycje: {result.ItemCount}.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(M2Plan), new { startDate, days });
    }

    [HttpPost("m2-plan/refresh-range")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshM2PlanRange(DateOnly startDate, int days = 7)
    {
        try
        {
            var result = await productionService.RefreshProductionPlansFromM2Async(startDate, days, GetOperatorName());
            TempData["Success"] =
                $"Refresh zakresu M2: utworzone {result.CreatedCount}, odświeżone {result.RefreshedCount}, " +
                $"pominięte {result.SkippedCount}, zablokowane {result.BlockedCount}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(M2Plan), new { startDate, days });
    }

    [HttpGet("warehouse-demand")]
    public async Task<IActionResult> WarehouseDemand([FromQuery] WarehouseDemandFilterDto filter)
    {
        if (filter.StartDate == default)
        {
            filter.StartDate = DateOnly.FromDateTime(DateTime.Today);
        }

        var demand = await warehouseDemandService.GetDemandAsync(filter);
        return View(demand);
    }

    [HttpGet("warehouse-demand.csv")]
    public async Task<IActionResult> WarehouseDemandCsv([FromQuery] WarehouseDemandFilterDto filter)
    {
        filter.ExportAll = true;
        var demand = await warehouseDemandService.GetDemandAsync(filter);
        var csv = BuildWarehouseDemandCsv(demand);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var fileName = $"warehouse-demand-{demand.StartDate:yyyyMMdd}-{demand.StartDate.AddDays(demand.RangeDays - 1):yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("warehouse-demand/print")]
    public async Task<IActionResult> WarehouseDemandPrint([FromQuery] WarehouseDemandFilterDto filter)
    {
        filter.ExportAll = true;
        var demand = await warehouseDemandService.GetDemandAsync(filter);
        return View("WarehouseDemandPrint", demand);
    }

    [HttpGet("cooking-cards")]
    public IActionResult CookingCards(DateOnly? date, string? search, string? status, int page = 1, int pageSize = 25)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return RedirectToAction(nameof(Index), new
        {
            date = targetDate.ToString("yyyy-MM-dd"),
            search,
            status,
            page,
            pageSize,
        });
    }

    // ── Karta gotowania ──────────────────────────────────────

    /// <summary>
    /// Wyświetla kartę gotowania (receptura + ilości) dla pozycji planu.
    /// GET /production/cooking-card/12
    /// </summary>
    [HttpGet("cooking-card")]
    [HttpGet("cooking-card/{planItemId:int}")]
    public async Task<IActionResult> CookingCard(int planItemId = 0)
    {
        if (planItemId == 0)
        {
            TempData["Error"] = "Brak podanego identyfikatora pozycji planu.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var cardDto = await productionService.GetCookingCardAsync(planItemId);
            return View(cardDto);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("cooking-card/{planItemId:int}/components/{recipeComponentVersionId:int}")]
    public async Task<IActionResult> CookingComponent(int planItemId, int recipeComponentVersionId)
    {
        try
        {
            var card = await productionService.GetCookingComponentCardAsync(planItemId, recipeComponentVersionId);
            card.Session = await cookingSessionService.GetComponentSessionAsync(planItemId, recipeComponentVersionId);
            return View(card);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(CookingCard), new { planItemId });
        }
    }

    [HttpPost("cooking-card/{planItemId:int}/components/{recipeComponentVersionId:int}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCookingComponent(int planItemId, int recipeComponentVersionId)
    {
        try
        {
            await cookingSessionService.StartComponentSessionAsync(planItemId, recipeComponentVersionId, GetOperatorName());
            TempData["Success"] = "Sesja gotowania skladowej zostala uruchomiona.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CookingComponent), new { planItemId, recipeComponentVersionId });
    }

    [HttpPost("cooking-card/{planItemId:int}/components/{recipeComponentVersionId:int}/steps/{stepId:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCookingComponentStep(
        int planItemId,
        int recipeComponentVersionId,
        int stepId,
        bool isChecked,
        decimal? actualValue,
        string? actualUnit,
        string? notes)
    {
        try
        {
            await cookingSessionService.ToggleStepAsync(new ToggleCookingStepRequest
            {
                ProductionPlanItemId = planItemId,
                RecipeComponentVersionId = recipeComponentVersionId,
                StepId = stepId,
                IsChecked = isChecked,
                ActualValue = actualValue,
                ActualUnit = actualUnit,
                Notes = notes,
                OperatorName = GetOperatorName(),
            });
            TempData["Success"] = isChecked ? "Krok zostal odznaczony." : "Krok zostal cofniety.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CookingComponent), new { planItemId, recipeComponentVersionId });
    }

    [HttpPost("cooking-card/{planItemId:int}/components/{recipeComponentVersionId:int}/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteCookingComponent(int planItemId, int recipeComponentVersionId)
    {
        try
        {
            await cookingSessionService.CompleteComponentSessionAsync(planItemId, recipeComponentVersionId, GetOperatorName());
            TempData["Success"] = "Skladowa zostala oznaczona jako ukonczona.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CookingComponent), new { planItemId, recipeComponentVersionId });
    }

    // ── Zatwierdzanie gotowania ──────────────────────────────

    /// <summary>
    /// Zatwierdza wykonanie pozycji planu z rzeczywistą ilością.
    /// POST /production/approve-cooking
    /// </summary>
    [HttpPost("approve-cooking")]
    [Authorize(Roles = "KitchenManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCooking(int planItemId, decimal actualQuantity)
    {
        try
        {
            await productionService.ApproveCookingAsync(planItemId, actualQuantity);
            TempData["Success"] = "Gotowanie zatwierdzone.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CookingCard), new { planItemId });
    }

    [HttpPost("adjustments/request")]
    [Authorize(Roles = "Kitchen,KitchenManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAdjustmentApproval(
        int planItemId,
        string adjustmentType,
        decimal requestedValue,
        string reason)
    {
        try
        {
            var approval = await productionService.RequestProductionAdjustmentApprovalAsync(new ProductionAdjustmentApprovalRequestDto
            {
                ProductionPlanItemId = planItemId,
                AdjustmentType = adjustmentType,
                RequestedValue = requestedValue,
                Reason = reason,
                RequestedBy = GetOperatorName(),
            });
            TempData["Success"] = $"Zgłoszono korektę #{approval.Id} do akceptacji managera.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CookingCard), new { planItemId });
    }

    [HttpPost("adjustments/{approvalId:int}/approve")]
    [Authorize(Roles = "KitchenManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAdjustment(int approvalId, int planItemId, string? approvalNote)
    {
        try
        {
            var approval = await productionService.ApproveProductionAdjustmentAsync(new ProductionAdjustmentApprovalDecisionDto
            {
                ApprovalId = approvalId,
                ApprovedBy = GetOperatorName(),
                ApprovalNote = approvalNote,
            });
            TempData["Success"] =
                $"Zaakceptowano korektę #{approval.Id}: {approval.PlannedValue:0.##} -> {approval.RequestedValue:0.##} {approval.Unit}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(CookingCard), new { planItemId });
    }

    // ── Produkcja półproduktów ────────────────────────────────

    /// <summary>
    /// Uruchamia dedukcję składników (FEFO) dla całego planu.
    /// POST /production/produce/5
    /// </summary>
    [HttpPost("produce/{planId:int}")]
    [Authorize(Roles = "KitchenManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProduceSemiFinished(int planId)
    {
        try
        {
            await productionService.ProduceSemiFinishedAsync(planId);
        TempData["Success"] = "Składniki zdjęte z magazynu wg FEFO. Plan uruchomiony.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Plan), new { planId });
    }

    // ── Foliowanie dań (Kitchen Foil Printing) ───────────────

    /// <summary>
    /// Wyświetla listę posiłków do zafoliowania na dany dzień.
    /// GET /production/foil-printing
    /// </summary>
    [HttpGet("foil-printing")]
    public async Task<IActionResult> FoilPrinting([FromQuery] FoilLabelFilterDto filter)
    {
        if (filter.Date == default)
        {
            filter.Date = DateOnly.FromDateTime(DateTime.Today);
        }

        try
        {
            var requestedBy = User.Identity?.Name ?? "Kuchnia";
            await packingSynchronizationService.EnsureSessionsForDateAsync(filter.Date, requestedBy);
            var preparation = await packingService.EnsureFoilBoxesForDateAsync(filter.Date);
            if (preparation.Errors.Count > 0)
            {
                TempData["Error"] = "Nie wszystkie pudełka do foliowania zostały przygotowane: "
                    + string.Join(" | ", preparation.Errors.Take(3));
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        var dashboard = await packingService.GetFoilLabelDashboardAsync(filter);
        return View(dashboard);
    }

    [HttpGet("rework")]
    public async Task<IActionResult> Rework([FromQuery] KitchenReworkFilterViewModel filter)
    {
        var selectedDate = filter.Date == default
            ? DateOnly.FromDateTime(DateTime.Today)
            : filter.Date;
        filter.Date = selectedDate;
        filter.Search = NormalizeReworkSearch(filter.Search);
        filter.Status = NormalizeReworkStatus(filter.Status);
        filter.Page = Math.Max(filter.Page, 1);
        filter.PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 10, 100);

        var allIncidents = (await packingIncidentService.GetKitchenReworkAsync(selectedDate)).ToList();
        var filteredIncidents = allIncidents
            .Where(incident => MatchesReworkFilter(incident, filter))
            .OrderByDescending(incident => incident.ReportedAt)
            .ThenByDescending(incident => incident.Id)
            .ToList();

        var totalPages = filteredIncidents.Count == 0
            ? 0
            : (int)Math.Ceiling((double)filteredIncidents.Count / filter.PageSize);
        filter.Page = totalPages == 0 ? 1 : Math.Min(filter.Page, totalPages);

        return View(new KitchenReworkViewModel
        {
            Filter = filter,
            SelectedDate = selectedDate,
            Incidents = filteredIncidents,
            Page = new PagedResultDto<PackingIncidentDto>
            {
                Items = filteredIncidents
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList(),
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = filteredIncidents.Count,
            },
        });
    }

    [HttpPost("rework/{incidentId:int}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartRework(int incidentId, DateOnly date)
    {
        await packingIncidentService.MarkKitchenInProgressAsync(new HandlePackingIncidentRequest
        {
            IncidentId = incidentId,
            Notes = "Kuchnia rozpoczęła ponowne przygotowanie.",
        });

        TempData["Success"] = $"Oznaczono zgłoszenie #{incidentId} jako rozpoczęte.";
        return RedirectToAction(nameof(Rework), new { date });
    }

    [HttpPost("rework/{incidentId:int}/prepared")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReworkPrepared(int incidentId, DateOnly date)
    {
        await packingIncidentService.MarkKitchenPreparedAsync(new HandlePackingIncidentRequest
        {
            IncidentId = incidentId,
            Notes = "Kuchnia oznaczyła zamiennik jako przygotowany.",
        });

        TempData["Success"] = $"Oznaczono zgłoszenie #{incidentId} jako przygotowane.";
        return RedirectToAction(nameof(Rework), new { date });
    }

    /// <summary>
    /// Drukuje etykietę foliową na pudełko i ustawia status na FoilPrinted.
    /// GET /production/foil-label/12
    /// </summary>
    [HttpGet("foil-label/{packingItemId:int}")]
    public async Task<IActionResult> FoilLabel(int packingItemId)
    {
        try
        {
            var label = await packingService.GetLatestFoilLabelAsync(packingItemId);
            if (label is null)
            {
                TempData["Error"] = "Brak wygenerowanej etykiety produktowej. Użyj przycisku Drukuj z listy foliowania.";
                return RedirectToAction(nameof(FoilPrinting));
            }

            return View(label);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(FoilPrinting));
        }
    }

    [HttpPost("foil-label/{packingItemId:int}/print")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintFoilLabel(int packingItemId, string? reprintReason, DateOnly? returnDate)
    {
        var operatorName = User.Identity?.Name ?? "Kuchnia";
        try
        {
            var label = await packingService.PrintFoilLabelAsync(packingItemId, operatorName, reprintReason);
            return View(nameof(FoilLabel), label);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(FoilPrinting), new { date = returnDate });
        }
    }

    /// <summary>
    /// Zbiorczy wydruk etykiet foliowych (bulk).
    /// GET /production/foil-labels-bulk?ids=1,2,3
    /// </summary>
    [HttpPost("foil-labels-bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FoilLabelsBulk(string ids, DateOnly? returnDate)
    {
        var operatorName = User.Identity?.Name ?? "Kuchnia";
        if (string.IsNullOrWhiteSpace(ids))
        {
            TempData["Error"] = "Nie wybrano żadnych etykiet do druku.";
            return RedirectToAction(nameof(FoilPrinting), new { date = returnDate });
        }

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .ToList();

        var labels = new List<PackingLabelDto>();
        var errors = new List<string>();
        foreach (var id in idList)
        {
            try
            {
                var label = await packingService.PrintFoilLabelAsync(id, operatorName);
                labels.Add(label);
            }
            catch (Exception ex)
            {
                errors.Add($"Pudelko #{id}: {ex.Message}");
                // Ignorujemy pojedyncze błędy generowania w pętli bulk
            }
        }

        if (labels.Count == 0 && errors.Count > 0)
        {
            TempData["Error"] = "Nie wydrukowano zadnej etykiety: " + string.Join(" | ", errors.Take(3));
            return RedirectToAction(nameof(FoilPrinting), new { date = returnDate });
        }

        ViewBag.Errors = errors;
        return View(labels);
    }

    private static string? NormalizeReworkSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static string? NormalizeReworkStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<PackingIncidentStatus>(status.Trim(), ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : null;
    }

    private static bool MatchesReworkFilter(PackingIncidentDto incident, KitchenReworkFilterViewModel filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Status)
            && !string.Equals(incident.Status.ToString(), filter.Status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Search)
            && !ReworkSearchText(incident).Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string ReworkSearchText(PackingIncidentDto incident)
        => string.Join(" ", new[]
        {
            incident.Id.ToString(),
            incident.ClientPublicId,
            incident.DeliveryCalendarId?.ToString(),
            incident.PackingSessionId.ToString(),
            incident.MealName,
            incident.BoxCode,
            incident.ReplacementPackingItemId?.ToString(),
            incident.ReasonSummary,
            incident.Description,
            incident.Status.ToString(),
            incident.ReplacementPackingItemStatus?.ToString(),
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string BuildWarehouseDemandCsv(WarehouseDemandDto demand)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Typ;Zasob;Wymagane;Jednostka;Dostepne;Brak;Ryzyko;Tryb FEFO;StockItemId;WarehouseCategoryId;Alokacja FEFO;Zrodla");
        foreach (var row in demand.Rows)
        {
            var allocations = string.Join(" | ", row.FefoAllocations.Select(allocation =>
                $"#{allocation.BatchId} {allocation.AllocatedQuantity:0.###}/{allocation.AvailableQuantity:0.###} {allocation.Unit}"));
            builder.AppendLine(string.Join(";",
            [
                Csv(row.ResourceType),
                Csv(row.ResourceName),
                Csv(row.RequiredQuantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(row.Unit),
                Csv(row.AvailableQuantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(row.ShortageQuantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                Csv(row.RiskLabel),
                Csv(row.SelectionMode),
                Csv(row.StockItemId?.ToString() ?? string.Empty),
                Csv(row.WarehouseCategoryId?.ToString() ?? string.Empty),
                Csv(allocations),
                Csv(string.Join(", ", row.SourceMeals)),
            ]));
        }

        return builder.ToString();
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private string GetOperatorName()
        => User.Identity?.Name ?? "Kuchnia";
}
