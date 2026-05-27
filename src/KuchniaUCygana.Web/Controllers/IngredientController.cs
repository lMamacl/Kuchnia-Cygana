using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("ingredients")]
public sealed class IngredientController : Controller
{
    private readonly IIngredientManagementService _ingredientService;

    public IngredientController(IIngredientManagementService ingredientService)
    {
        _ingredientService = ingredientService;
    }

    // GET
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Katalog skladnikow";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Wspolny katalog M2/M3 do wyszukiwania i zarzadzania skladnikami.";
        return View();
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Dodaj skladnik";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Szkielet formularza tworzenia skladnika.";
        return View();
    }

    [HttpGet("{id:int?}")]
    public IActionResult Details(int? id)
    {
        ViewData["Title"] = "Edycja skladnika";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder edycji skladnika #{id}."
            : "Placeholder edycji skladnika bez wybranego identyfikatora.";
        return View();
    }

    [HttpGet("nutrition")]
    [HttpGet("{id:int}/nutrition")]
    public IActionResult Nutrition(int? id)
    {
        ViewData["Title"] = "Wartosci odzywcze";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder wartosci odzywczych skladnika #{id}."
            : "Placeholder wartosci odzywczych skladnika.";
        return View();
    }
    // POST
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IngredientDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _ingredientService.CreateAsync(dto);
        TempData["Success"] = "Sk³adnik dodany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, IngredientDto dto)
    {
        if (id != dto.Id) return BadRequest();
        if (!ModelState.IsValid) return View(dto);
        await _ingredientService.UpdateAsync(dto);
        TempData["Success"] = "Sk³adnik zaktualizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _ingredientService.DeleteAsync(id);
        if (!deleted) TempData["Error"] = "Nie mo¿na usun¹æ – sk³adnik u¿ywany w recepturach.";
        else TempData["Success"] = "Sk³adnik usuniêty.";
        return RedirectToAction(nameof(Index));
    }
}
