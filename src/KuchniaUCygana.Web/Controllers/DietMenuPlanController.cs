using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Dietitian,Admin")]
[Route("diet-editor/menu-plan")]
public sealed class DietMenuPlanController : Controller
{
    private static readonly string[] Slots = ["Breakfast", "Snack1", "Lunch", "Snack2", "Dinner"];

    private readonly IDietMenuPlanManagementService menuPlanService;
    private readonly IDietManagementService dietService;
    private readonly IMealManagementService mealService;

    public DietMenuPlanController(
        IDietMenuPlanManagementService menuPlanService,
        IDietManagementService dietService,
        IMealManagementService mealService)
    {
        this.menuPlanService = menuPlanService;
        this.dietService = dietService;
        this.mealService = mealService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? startDate, int days = 7)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.Today);
        var model = await this.menuPlanService.GetWeekAsync(start, days);
        this.SetHeader("Plan menu", "Tygodniowy plan M2 do publikacji snapshotu.");
        return this.View("~/Views/DietEditor/MenuPlanWeek.cshtml", model);
    }

    [HttpGet("day/{date}")]
    public async Task<IActionResult> Day(DateOnly date)
    {
        var model = await this.menuPlanService.GetDayAsync(date);
        await this.LoadLookupsAsync();
        this.SetHeader("Dzien menu", $"Edycja planu na {date:yyyy-MM-dd}.");
        return this.View("~/Views/DietEditor/MenuPlanDay.cshtml", model);
    }

    [HttpPost("day/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDay(CreateDietMenuPlanRequest request)
    {
        try
        {
            await this.menuPlanService.CreateDayAsync(request);
            this.TempData["Success"] = "Draft dnia menu utworzony.";
        }
        catch (Exception ex)
        {
            this.TempData["Error"] = ex.Message;
        }

        return this.RedirectToAction(nameof(Day), new { date = request.PlanDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("day/{planId:int}/items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(int planId, AddDietMenuPlanItemRequest request, DateOnly planDate)
    {
        request.DietMenuPlanId = planId;
        try
        {
            await this.menuPlanService.AddItemAsync(request);
            this.TempData["Success"] = "Pozycja planu dodana.";
        }
        catch (Exception ex)
        {
            this.TempData["Error"] = ex.Message;
        }

        return this.RedirectToAction(nameof(Day), new { date = planDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("day/{planId:int}/items/{itemId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(int planId, int itemId, UpdateDietMenuPlanItemRequest request, DateOnly planDate)
    {
        request.Id = itemId;
        request.DietMenuPlanId = planId;
        try
        {
            await this.menuPlanService.UpdateItemAsync(request);
            this.TempData["Success"] = "Pozycja planu zaktualizowana.";
        }
        catch (Exception ex)
        {
            this.TempData["Error"] = ex.Message;
        }

        return this.RedirectToAction(nameof(Day), new { date = planDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("day/{planId:int}/items/{itemId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int itemId, DateOnly planDate)
    {
        try
        {
            await this.menuPlanService.DeleteItemAsync(itemId);
            this.TempData["Success"] = "Pozycja planu usunieta.";
        }
        catch (Exception ex)
        {
            this.TempData["Error"] = ex.Message;
        }

        return this.RedirectToAction(nameof(Day), new { date = planDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("day/{planId:int}/copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyDay(int planId, CopyDietMenuDayRequest request, DateOnly returnDate)
    {
        request.SourcePlanId = planId;
        request.ClearTargetDraft = true;
        try
        {
            await this.menuPlanService.CopyDayAsync(request);
            this.TempData["Success"] = "Dzien menu skopiowany.";
            return this.RedirectToAction(nameof(Day), new { date = request.TargetDate.ToString("yyyy-MM-dd") });
        }
        catch (Exception ex)
        {
            this.TempData["Error"] = ex.Message;
        }

        return this.RedirectToAction(nameof(Day), new { date = returnDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("day/{planId:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int planId, DateOnly planDate)
    {
        try
        {
            await this.menuPlanService.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = planId });
            this.TempData["Success"] = "Plan dnia opublikowany.";
        }
        catch (Exception ex)
        {
            this.TempData["Error"] = ex.Message;
        }

        return this.RedirectToAction(nameof(Day), new { date = planDate.ToString("yyyy-MM-dd") });
    }

    [HttpGet("lookups/meals")]
    public async Task<IActionResult> LookupMeals([FromQuery] string? query)
    {
        var meals = await this.mealService.SearchPlanningMealsAsync(query, 20);
        return this.Json(meals);
    }

    [HttpGet("lookups/meals/{mealId:int}/variants")]
    public async Task<IActionResult> LookupMealVariants(int mealId)
    {
        var variants = await this.mealService.GetPlanningMealVariantOptionsAsync(mealId);
        return this.Json(variants);
    }

    private async Task LoadLookupsAsync()
    {
        this.ViewBag.Diets = await this.dietService.GetActiveDietsAsync();
        this.ViewBag.Slots = Slots;
    }

    private void SetHeader(string title, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = "Plan menu";
        this.ViewData["Description"] = description;
    }
}
