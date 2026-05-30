using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Dietitian,Admin")]
[Route("meals")]
public sealed class MealsController : Controller
{
    private readonly IMealManagementService _mealService;
    private readonly IImageManagementService _imageService;
    private readonly IAiDescriptionService _aiService;
    private readonly ICategoryService _categoryService;
    private readonly IIngredientManagementService _ingredientService;

    public MealsController(
        IMealManagementService mealService,
        IImageManagementService imageService,
        IAiDescriptionService aiService,
        ICategoryService categoryService,
        IIngredientManagementService ingredientService)
    {
        _mealService = mealService;
        _imageService = imageService;
        _aiService = aiService;
        _categoryService = categoryService;
        _ingredientService = ingredientService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var meals = await _mealService.GetPublishedMealsAsync();
        return View(meals);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _categoryService.GetAllAsync();
        return View();
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMealRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _categoryService.GetAllAsync();
            return View(request);
        }

        await _mealService.CreateMealAsync(request);
        TempData["Success"] = "Posiłek został utworzony.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var meal = await _mealService.GetMealWithDetailsAsync(id);
        if (meal == null) return NotFound();
        return View(meal);
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var meal = await _mealService.GetMealAsync(id);
        if (meal == null) return NotFound();
        ViewBag.Categories = await _categoryService.GetAllAsync();
        return View(meal);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateMealRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _categoryService.GetAllAsync();
            return View(request);
        }

        await _mealService.UpdateMealAsync(id, request);
        TempData["Success"] = "Posiłek zaktualizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _mealService.DeleteMealAsync(id);
        TempData["Success"] = "Posiłek usunięty.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("publish/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        await _mealService.PublishMealAsync(id);
        TempData["Success"] = "Posiłek opublikowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("archive/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        await _mealService.ArchiveMealAsync(id);
        TempData["Success"] = "Posiłek zarchiwizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("upload-image/{mealId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(int mealId, IFormFile file, bool isMain = false)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Nie wybrano pliku.";
            return RedirectToAction(nameof(Details), new { id = mealId });
        }

        using var stream = file.OpenReadStream();
        await _imageService.UploadAsync(mealId, stream, file.FileName, isMain);
        TempData["Success"] = "Zdjęcie dodane.";
        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("set-main-image/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMainImage(int imageId, int mealId)
    {
        await _imageService.SetMainAsync(imageId);
        TempData["Success"] = "Główne zdjęcie zmienione.";
        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("delete-image/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int mealId)
    {
        await _imageService.DeleteAsync(imageId);
        TempData["Success"] = "Zdjęcie usunięte.";
        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("generate-description/{mealId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateDescription(int mealId)
    {
        var response = await _aiService.GenerateDescriptionAsync(new AiGenerateDescriptionRequest { MealId = mealId });
        TempData["Success"] = "Opis marketingowy wygenerowany przez AI.";
        return RedirectToAction(nameof(Details), new { id = mealId });
    }
}
