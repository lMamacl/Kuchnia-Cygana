using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("menu")]
public sealed class MenuController : Controller
{
    private readonly IDietCatalogProvider dietCatalogProvider;
    private readonly IDietDataProvider dietDataProvider;
    private readonly IDietOrderingService dietOrderingService;

    public MenuController(
        IDietCatalogProvider dietCatalogProvider,
        IDietDataProvider dietDataProvider,
        IDietOrderingService dietOrderingService)
    {
        this.dietCatalogProvider = dietCatalogProvider;
        this.dietDataProvider = dietDataProvider;
        this.dietOrderingService = dietOrderingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? search)
    {
        ViewData["Title"] = "Katalog diet";

        var catalog = await dietCatalogProvider.GetCurrentCatalogAsync(new DietCatalogQuery
        {
            SearchTerm = search,
        });

        return View(new MenuCatalogViewModel
        {
            SearchTerm = search,
            BasePricePerDay = dietOrderingService.BasePricePerDay,
            Diets = catalog.Diets.Select(MapDiet).ToList(),
        });
    }

    [HttpGet("diet/{id:int}")]
    public async Task<IActionResult> Diet(int id, [FromQuery] int? variantId, [FromQuery] string? startDate)
    {
        var diet = await dietCatalogProvider.GetDietAsync(id);
        if (diet is null || !diet.IsActive || !IsPublishedOrActive(diet.Status))
            return NotFound();

        var mappedDiet = MapDiet(diet);
        var selectedVariant = ResolveSelectedVariant(mappedDiet, variantId);
        var weekStart = ResolveWeekStartDate(startDate);
        IReadOnlyList<DietPlanEntry> weekPlan = selectedVariant is null
            ? Array.Empty<DietPlanEntry>()
            : (await dietDataProvider.Get7DayPlanAsync(weekStart))
                .Where(item => item.DietVariantId == selectedVariant.DietVariantId)
                .OrderBy(item => item.PlanDate)
                .ThenBy(item => item.SortOrder)
                .ThenBy(item => item.MealSlot)
                .ToList();

        ViewData["Title"] = mappedDiet.Name;

        return View(new MenuDietDetailsViewModel
        {
            Diet = mappedDiet,
            SelectedVariant = selectedVariant,
            WeekStartDate = weekStart,
            WeekPlan = weekPlan,
        });
    }

    [HttpGet("diet/{dietId:int}/week-plan")]
    public async Task<IActionResult> WeekPlan(int dietId, [FromQuery] int? variantId, [FromQuery] string? startDate)
    {
        var diet = await dietCatalogProvider.GetDietAsync(dietId);
        if (diet is null || !diet.IsActive || !IsPublishedOrActive(diet.Status))
            return NotFound();

        var mappedDiet = MapDiet(diet);
        var selectedVariant = ResolveSelectedVariant(mappedDiet, variantId);
        var weekStart = ResolveWeekStartDate(startDate);
        IReadOnlyList<DietPlanEntry> weekPlan = selectedVariant is null
            ? Array.Empty<DietPlanEntry>()
            : (await dietDataProvider.Get7DayPlanAsync(weekStart))
                .Where(item => item.DietVariantId == selectedVariant.DietVariantId)
                .OrderBy(item => item.PlanDate)
                .ThenBy(item => item.SortOrder)
                .ThenBy(item => item.MealSlot)
                .ToList();

        ViewData["Title"] = $"Jadlospis - {mappedDiet.Name}";

        return View(new MenuDietDetailsViewModel
        {
            Diet = mappedDiet,
            SelectedVariant = selectedVariant,
            WeekStartDate = weekStart,
            WeekPlan = weekPlan,
        });
    }

    [HttpGet("week-plan/{dietId:int?}")]
    public IActionResult LegacyWeekPlan(int? dietId)
    {
        if (!dietId.HasValue)
            return RedirectToAction(nameof(Index));

        return RedirectToAction(nameof(WeekPlan), new { dietId = dietId.Value });
    }

    private MenuDietViewModel MapDiet(DietCatalogItemDto diet)
        => new()
        {
            DietId = diet.DietId,
            Name = diet.Name,
            Description = diet.Description,
            MarketingDescription = diet.MarketingDescription,
            ThumbnailUrl = diet.ThumbnailUrl,
            Variants = diet.Variants
                .Select(variant => new MenuDietVariantViewModel
                {
                    DietVariantId = variant.DietVariantId,
                    DietId = variant.DietId,
                    Name = variant.Name,
                    TargetCalories = variant.TargetCalories,
                    PriceMultiplier = variant.PriceMultiplier,
                    PricePerDay = dietOrderingService.CalculatePricePerDay(variant),
                    IsDefault = variant.IsDefault,
                    IsAvailable = variant.IsAvailable,
                })
                .ToList(),
        };

    private static MenuDietVariantViewModel? ResolveSelectedVariant(MenuDietViewModel diet, int? variantId)
    {
        if (variantId.HasValue)
        {
            var requested = diet.Variants.FirstOrDefault(v => v.DietVariantId == variantId.Value && v.IsAvailable);
            if (requested is not null)
                return requested;
        }

        return diet.DefaultVariant;
    }

    private static DateOnly ResolveWeekStartDate(string? value)
    {
        if (DateOnly.TryParse(value, out var parsed))
            return parsed;

        return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    }

    private static bool IsPublishedOrActive(string status)
        => string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
}
