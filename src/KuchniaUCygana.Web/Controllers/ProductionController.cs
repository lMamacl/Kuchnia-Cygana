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

    public ProductionController(IProductionService productionService, IPackingService packingService)
    {
        this.productionService = productionService;
        this.packingService = packingService;
    }

    // ── Plan dnia ─────────────────────────────────────────────

    /// <summary>
    /// Widok główny produkcji — plan produkcji na wybrany dzień.
    /// GET /production
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var plan = await productionService.GetDailyPlanByDateAsync(targetDate);

        var viewModel = new ProductionDashboardViewModel
        {
            SelectedDate = targetDate,
            DailyPlan = plan
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
        await productionService.ApproveCookingAsync(planItemId, actualQuantity);
        TempData["Success"] = "Gotowanie zatwierdzone.";
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
        await productionService.ProduceSemiFinishedAsync(planId);
        TempData["Success"] = "Składniki zdjęte z magazynu wg FEFO. Plan uruchomiony.";
        return RedirectToAction(nameof(Plan), new { planId });
    }

    // ── Foliowanie dań (Kitchen Foil Printing) ───────────────

    /// <summary>
    /// Wyświetla listę posiłków do zafoliowania na dany dzień.
    /// GET /production/foil-printing
    /// </summary>
    [HttpGet("foil-printing")]
    public async Task<IActionResult> FoilPrinting(DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var sessions = (await packingService.GetSessionsByDateAsync(targetDate)).ToList();

        // Automatycznie generujemy pudełka dla dzisiejszych zamówień, jeśli nie zostały jeszcze utworzone
        foreach (var session in sessions)
        {
            if (session.Items.Count == 0 && session.OrderId.HasValue)
            {
                try
                {
                    await packingService.PrepareOrderBoxesAsync(session.Id);
                }
                catch (Exception)
                {
                    // Ignorujemy błędy generowania dla pojedynczych sesji (np. brak diety w bazie)
                }
            }
        }

        // Pobieramy sesje ponownie, tym razem z załadowanymi pudełkami
        sessions = (await packingService.GetSessionsByDateAsync(targetDate)).ToList();

        ViewBag.SelectedDate = targetDate;
        return View(sessions);
    }

    /// <summary>
    /// Drukuje etykietę foliową na pudełko i ustawia status na FoilPrinted.
    /// GET /production/foil-label/12
    /// </summary>
    [HttpGet("foil-label/{packingItemId:int}")]
    public async Task<IActionResult> FoilLabel(int packingItemId)
    {
        var operatorName = User.Identity?.Name ?? "Kuchnia";
        try
        {
            var label = await packingService.PrintFoilLabelAsync(packingItemId, operatorName);
            return View(label);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(FoilPrinting));
        }
    }

    /// <summary>
    /// Zbiorczy wydruk etykiet foliowych (bulk).
    /// GET /production/foil-labels-bulk?ids=1,2,3
    /// </summary>
    [HttpGet("foil-labels-bulk")]
    public async Task<IActionResult> FoilLabelsBulk(string ids)
    {
        var operatorName = User.Identity?.Name ?? "Kuchnia";
        if (string.IsNullOrWhiteSpace(ids))
        {
            TempData["Error"] = "Nie wybrano żadnych etykiet do druku.";
            return RedirectToAction(nameof(FoilPrinting));
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
