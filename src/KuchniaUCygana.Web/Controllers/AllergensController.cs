using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("allergens")]
public sealed class AllergensController : Controller
{
    private readonly IAllergenManagementService _allergenService;

    public AllergensController(IAllergenManagementService allergenService)
    {
        _allergenService = allergenService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var allergens = await _allergenService.GetAllAsync();
        return View(allergens);
    }

    [HttpGet("create")]
    public IActionResult Create() => View();

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AllergenDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        await _allergenService.CreateAsync(dto);
        TempData["Success"] = "Alergen dodany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var allergen = await _allergenService.GetAsync(id);
        if (allergen == null) return NotFound();
        return View(allergen);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AllergenDto dto)
    {
        if (id != dto.Id) return BadRequest();
        if (!ModelState.IsValid) return View(dto);
        await _allergenService.UpdateAsync(dto);
        TempData["Success"] = "Alergen zaktualizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _allergenService.DeleteAsync(id);
        TempData["Success"] = "Alergen usunięty.";
        return RedirectToAction(nameof(Index));
    }
}
