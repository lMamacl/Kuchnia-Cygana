using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KuchniaUCygana.Web.Controllers;

[Route("ingredients")]
[Authorize(Roles = "Dietitian,Admin")]
public sealed class IngredientController : Controller
{
    private readonly IIngredientManagementService _ingredientService;
    private readonly IAllergenManagementService _allergenService;
    private readonly ICategoryService _categoryService;
    private readonly IWarehouseCategoryService _warehouseCategoryService;

    public IngredientController(
        IIngredientManagementService ingredientService,
        IAllergenManagementService allergenService,
        ICategoryService categoryService,
        IWarehouseCategoryService warehouseCategoryService)
    {
        _ingredientService = ingredientService;
        _allergenService = allergenService;
        _categoryService = categoryService;
        _warehouseCategoryService = warehouseCategoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] IngredientSearchFilterDto filter)
    {
        var ingredients = await _ingredientService.SearchAsync(filter);
        await LoadLookupsAsync(filter.FoodCategoryId, filter.WarehouseCategoryId);
        ViewBag.Filter = filter;
        ViewBag.SelectedAllergenId = filter.AllergenId;
        SetMetadata(
            "Katalog skladnikow",
            "Skladniki",
            "Wspolny katalog M2/M3 do wyszukiwania i zarzadzania skladnikami.");
        return View(ingredients);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        SetMetadata("Dodaj skladnik", "Skladniki", "Formularz tworzenia skladnika.");
        return View(new IngredientDto { IsActive = true, Nutrition = new NutritionFactDto() });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var ingredient = await _ingredientService.GetAsync(id);
        if (ingredient is null)
        {
            return NotFound();
        }

        await LoadLookupsAsync(ingredient.FoodCategoryId, ingredient.WarehouseCategoryId);
        SetMetadata("Edycja skladnika", "Skladniki", $"Edycja skladnika #{id}.");
        return View(ingredient);
    }

    [HttpGet("nutrition")]
    [HttpGet("{id:int}/nutrition")]
    public async Task<IActionResult> Nutrition(int? id)
    {
        var ingredient = id.HasValue ? await _ingredientService.GetAsync(id.Value) : null;
        SetMetadata(
            "Wartosci odzywcze",
            "Skladniki",
            id.HasValue
                ? $"Wartosci odzywcze skladnika #{id}."
                : "Wartosci odzywcze skladnika.");
        return View(ingredient);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IngredientDto dto)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(dto.FoodCategoryId, dto.WarehouseCategoryId);
            SetMetadata("Dodaj skladnik", "Skladniki", "Formularz tworzenia skladnika.");
            return View(dto);
        }

        await _ingredientService.CreateAsync(dto);
        TempData["Success"] = "Skladnik dodany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, IngredientDto dto)
    {
        if (id != dto.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(dto.FoodCategoryId, dto.WarehouseCategoryId);
            SetMetadata("Edycja skladnika", "Skladniki", $"Edycja skladnika #{id}.");
            return View("Details", dto);
        }

        await _ingredientService.UpdateAsync(dto);
        TempData["Success"] = "Skladnik zaktualizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _ingredientService.DeleteAsync(id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Skladnik usuniety."
            : "Nie mozna usunac skladnika, poniewaz jest uzywany w recepturach.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLookupsAsync(int? selectedFoodCategoryId = null, int? selectedWarehouseCategoryId = null)
    {
        var allergens = (await _allergenService.GetAllAsync()).ToList();
        var foodCategories = (await _categoryService.GetAllAsync()).ToList();
        var warehouseCategories = await _warehouseCategoryService.GetAllAsync();

        ViewBag.Allergens = allergens;
        ViewBag.FoodCategories = new SelectList(foodCategories, "Id", "Name", selectedFoodCategoryId);
        ViewBag.WarehouseCategories = new SelectList(warehouseCategories, "Id", "Name", selectedWarehouseCategoryId);
    }

    private void SetMetadata(string title, string section, string description)
    {
        ViewData["Title"] = title;
        ViewData["Section"] = section;
        ViewData["Description"] = description;
    }
}
