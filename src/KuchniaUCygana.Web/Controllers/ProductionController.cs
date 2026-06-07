using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
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
        var m2PlanOverview = await productionService.GetM2PlanOverviewAsync(filter.Date, 7);

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
    public async Task<IActionResult> Plan(int planId = 0)
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

        var planDto = await productionService.GetPlanByIdAsync(planId);
        if (planDto == null)
        {
            TempData["Error"] = $"Nie znaleziono planu o ID #{planId}.";
            return RedirectToAction(nameof(Index));
        }

        return View(planDto);
    }

    [HttpGet("m2-plan")]
    public async Task<IActionResult> M2Plan([FromQuery] M2PlanOverviewFilterDto filter)
    {
        if (filter.StartDate == default)
        {
            filter.StartDate = DateOnly.FromDateTime(DateTime.Today);
        }

        var overview = await productionService.GetM2PlanOverviewAsync(filter.StartDate, filter.Days);
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

        var demand = await warehouseDemandService.GetDemandAsync(filter.StartDate, filter.Days);
        return View(demand);
    }

    [HttpGet("cooking-cards")]
    public async Task<IActionResult> CookingCards(DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var plan = await productionService.GetDailyPlanByDateAsync(targetDate);
        return View(plan);
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
    public async Task<IActionResult> Rework(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return View(new KitchenReworkViewModel
        {
            SelectedDate = selectedDate,
            Incidents = await packingIncidentService.GetKitchenReworkAsync(selectedDate),
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

    private string GetOperatorName()
        => User.Identity?.Name ?? "Kuchnia";
}
