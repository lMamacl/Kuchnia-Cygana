using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.Interfaces;
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

    public ProductionController(IProductionService productionService)
    {
        this.productionService = productionService;
    }

    // ── Plan dnia ─────────────────────────────────────────────

    /// <summary>
    /// Widok główny produkcji — plan produkcji na wybrany dzień.
    /// GET /production
    /// </summary>
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
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
            return View("Index", request);
        }

        var plan = await productionService.GenerateDailyPlanAsync(request);
        TempData["Success"] = $"Plan produkcji na {request.ProductionDate:dd.MM.yyyy} został wygenerowany ({plan.Items.Count} pozycji).";
        return RedirectToAction(nameof(Plan), new { planId = plan.Id });
    }

    /// <summary>
    /// Widok szczegółowy planu produkcji.
    /// GET /production/plan/5
    /// </summary>
    [HttpGet("plan/{planId:int}")]
    public IActionResult Plan(int planId)
    {
        ViewBag.PlanId = planId;
        return View();
    }

    // ── Karta gotowania ──────────────────────────────────────

    /// <summary>
    /// Wyświetla kartę gotowania (receptura + ilości) dla pozycji planu.
    /// GET /production/cooking-card/12
    /// </summary>
    [HttpGet("cooking-card/{planItemId:int}")]
    public async Task<IActionResult> CookingCard(int planItemId)
    {
        var card = await productionService.GetCookingCardAsync(planItemId);
        return View(card);
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
}
