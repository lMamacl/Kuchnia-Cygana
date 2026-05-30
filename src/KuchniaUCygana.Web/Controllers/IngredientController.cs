using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("ingredients")]
[Authorize(Roles = "Dietitian,Admin")]
public sealed class IngredientController : Controller
{
    private readonly IIngredientManagementService _ingredientService;

    public IngredientController(IIngredientManagementService ingredientService)
    {
        _ingredientService = ingredientService;
    }

    // GET
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var ingredients = await _ingredientService.GetAllAsync();
        ViewData["Title"] = "Katalog skladnikow";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Wspolny katalog M2/M3 do wyszukiwania i zarzadzania skladnikami.";
        return View(ingredients);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Dodaj skladnik";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Szkielet formularza tworzenia skladnika.";
        return View(new IngredientDto { IsActive = true });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var ingredient = await _ingredientService.GetAsync(id);
        if (ingredient is null) return NotFound();

        ViewData["Title"] = "Edycja skladnika";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = $"Edycja skladnika #{id}.";
        return View(ingredient);
    }

    [HttpGet("nutrition")]
    [HttpGet("{id:int}/nutrition")]
    public async Task<IActionResult> Nutrition(int? id)
    {
        var ingredient = id.HasValue ? await _ingredientService.GetAsync(id.Value) : null;
        ViewData["Title"] = "Wartosci odzywcze";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Wartosci odzywcze skladnika #{id}."
            : "Placeholder wartosci odzywczych skladnika.";
        return View(ingredient);
    }

    // POST
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IngredientDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _ingredientService.CreateAsync(dto);
        TempData["Success"] = "Składnik dodany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, IngredientDto dto)
    {
        if (id != dto.Id) return BadRequest();
        if (!ModelState.IsValid) return View(dto);
        await _ingredientService.UpdateAsync(dto);
        TempData["Success"] = "Składnik zaktualizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _ingredientService.DeleteAsync(id);
        if (!deleted) TempData["Error"] = "Nie można usunąć – składnik używany w recepturach.";
        else TempData["Success"] = "Składnik usunięty.";
        return RedirectToAction(nameof(Index));
    }
}
