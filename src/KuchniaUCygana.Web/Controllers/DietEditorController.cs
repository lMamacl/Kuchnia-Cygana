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

    public DietEditorController(IDietManagementService dietService)
    {
        _dietService = dietService;
    }

    // GET

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var diets = await _dietService.GetActiveDietsAsync();
        ViewData["Title"] = "Edytor diet";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Dashboard dietetyka z list¹ diet i statusami publikacji.";
        return View(diets);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nowa dieta";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Szkielet kreatora nowej diety.";
        return View();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var diet = await _dietService.GetDietAsync(id);
        if (diet == null) return NotFound();
        ViewData["Title"] = "Szczegó³y diety";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = $"Szczegó³y diety #{id}.";
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
        if (!ModelState.IsValid) return View(request);
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
        await _dietService.AssignMealToVariantAsync(variantId, mealId, multiplier, sortOrder);
        TempData["Success"] = "Posi³ek przypisany do wariantu.";
        return RedirectToAction(nameof(Details), new { id = dietId });
    }
}
