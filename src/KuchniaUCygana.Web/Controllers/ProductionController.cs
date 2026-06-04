using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

/// <summary>
/// Kontroler produkcji — plan dnia, karty gotowania, zatwierdzanie.
/// TASK-M3-024 | Stanowisko: Kitchen | Szef: KitchenManager
/// </summary>
[Authorize(Roles = "Kitchen,KitchenManager,Admin")]
[Route("production")]
public sealed class ProductionController : Controller
{
    private readonly IProductionService productionService;
    private readonly IPackingService packingService;
    private readonly IPackingIncidentService packingIncidentService;
    private readonly IPackingSynchronizationService packingSynchronizationService;

    public ProductionController(
        IProductionService productionService,
        IPackingService packingService,
        IPackingIncidentService packingIncidentService,
        IPackingSynchronizationService packingSynchronizationService)
    {
        this.productionService = productionService;
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

        var viewModel = new ProductionDashboardViewModel
        {
            SelectedDate = dashboard.Filter.Date,
            DailyPlan = dashboard.Plan,
            Dashboard = dashboard,
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
        foreach (var id in idList)
        {
            try
            {
                var label = await packingService.PrintFoilLabelAsync(id, operatorName);
                labels.Add(label);
            }
            catch (Exception)
            {
                // Ignorujemy pojedyncze błędy generowania w pętli bulk
            }
        }

        return View(labels);
    }
}
