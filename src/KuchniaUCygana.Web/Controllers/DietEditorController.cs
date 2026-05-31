using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Dietitian,Admin")]
[Route("diet-editor")]
public sealed class DietEditorController : Controller
{
    private readonly IDietManagementService _dietService;
    private readonly IMealManagementService _mealService;
    private readonly IRecipeComponentManagementService _recipeComponentService;

    public DietEditorController(
        IDietManagementService dietService,
        IMealManagementService mealService,
        IRecipeComponentManagementService recipeComponentService)
    {
        _dietService = dietService;
        _mealService = mealService;
        _recipeComponentService = recipeComponentService;
    }

    // GET

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var diets = await _dietService.GetActiveDietsAsync();
        ViewData["Title"] = "Edytor diet";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Dashboard dietetyka z listą diet i statusami publikacji.";
        return View(diets);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nowa dieta";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Szkielet kreatora nowej diety.";
        return View(new CreateDietRequest
        {
            Variants =
            [
                new CreateDietVariantRequest
                {
                    Name = "Standard",
                    TargetCalories = 1800,
                    PriceMultiplier = 1m,
                    IsDefault = true,
                },
            ],
        });
    }

    [HttpGet("meals")]
    public IActionResult Meals()
    {
        return RedirectToAction("Index", "Meals");
    }

    [HttpGet("recipes")]
    public async Task<IActionResult> Recipes(string? query)
    {
        ViewData["Title"] = "Przepisy";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Wersjonowane przepisy-skladowe uzywane przez posilki.";
        ViewBag.Query = query;
        return View(await _recipeComponentService.SearchAsync(query));
    }

    [HttpGet("recipe")]
    [HttpGet("recipe/{mealId:int}")]
    public IActionResult Recipe(int? mealId, int? componentVersionId)
    {
        ViewData["Title"] = "Edycja przepisu";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = mealId.HasValue
            ? componentVersionId.HasValue
                ? $"Szkielet edycji przepisu dla posilku #{mealId.Value}, wersja skladowej #{componentVersionId.Value}."
                : $"Szkielet edycji przepisu dla posilku #{mealId.Value}."
            : "Wybierz posilek, aby przejsc do receptury.";
        return View();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var diet = await _dietService.GetDietAsync(id);
        if (diet == null) return NotFound();
        ViewData["Title"] = "Szczegóły diety";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = $"Szczegóły diety #{id}.";
        ViewBag.AvailableMeals = await _mealService.GetPublishedMealsAsync();
        return View(diet);
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var diet = await _dietService.GetDietAsync(id);
        if (diet == null) return NotFound();
        ViewData["Title"] = "Edycja diety";
        ViewData["Section"] = "Diety";
        return View(diet);
    }

    // POST

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDietRequest request)
    {
        if (!ModelState.IsValid) return View(request);
        await _dietService.CreateDietAsync(request);
        TempData["Success"] = "Dieta utworzona.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateDietRequest request)
    {
        if (!ModelState.IsValid)
        {
            var diet = await _dietService.GetDietAsync(id);
            return diet is null ? NotFound() : View(diet);
        }

        await _dietService.UpdateDietAsync(id, request);
        TempData["Success"] = "Dieta zaktualizowana.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("add-variant/{dietId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(int dietId, CreateDietVariantRequest request)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id = dietId });
        await _dietService.AddVariantAsync(dietId, request);
        TempData["Success"] = "Wariant dodany.";
        return RedirectToAction(nameof(Details), new { id = dietId });
    }

    // dietId przekazywany jako ukryte pole formularza (<input type="hidden" name="dietId" value="@Model.DietId" />)
    [HttpPost("assign-meal/{variantId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignMeal(int variantId, int dietId, int mealId, decimal multiplier, int sortOrder)
    {
        try
        {
            await _dietService.AssignMealToVariantAsync(variantId, mealId, multiplier, sortOrder);
            TempData["Success"] = "Posiłek przypisany do wariantu.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = dietId });
    }
}
