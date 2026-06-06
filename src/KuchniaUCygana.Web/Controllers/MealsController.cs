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
    private readonly IRecipeComponentManagementService _recipeComponentService;

    public MealsController(
        IMealManagementService mealService,
        IImageManagementService imageService,
        IAiDescriptionService aiService,
        ICategoryService categoryService,
        IIngredientManagementService ingredientService,
        IRecipeComponentManagementService recipeComponentService)
    {
        _mealService = mealService;
        _imageService = imageService;
        _aiService = aiService;
        _categoryService = categoryService;
        _ingredientService = ingredientService;
        _recipeComponentService = recipeComponentService;
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

    [HttpPost("{mealId:int}/variants")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVariant(int mealId, CreateMealVariantRequest request)
    {
        request.MealId = mealId;
        try
        {
            await _mealService.CreateMealVariantAsync(request);
            TempData["Success"] = "Wariant posilku zostal utworzony.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVariant(int mealId, int variantId, UpdateMealVariantRequest request)
    {
        try
        {
            await _mealService.UpdateMealVariantAsync(variantId, request);
            TempData["Success"] = "Wariant posilku zostal zapisany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}/components")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVariantComponent(
        int mealId,
        int variantId,
        SaveMealVariantComponentRequest request)
    {
        try
        {
            await _mealService.SaveMealVariantComponentAsync(variantId, request);
            TempData["Success"] = "Skladowa wariantu zostala zapisana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}/components/{componentId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariantComponent(int mealId, int variantId, int componentId)
    {
        await _mealService.DeleteMealVariantComponentAsync(componentId);
        TempData["Success"] = "Skladowa wariantu zostala usunieta.";
        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}/packaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVariantPackaging(
        int mealId,
        int variantId,
        SaveMealVariantPackagingRequest request)
    {
        try
        {
            await _mealService.SaveMealVariantPackagingAsync(variantId, request);
            TempData["Success"] = "Opakowanie wariantu zostalo zapisane.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}/packaging/{packagingId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariantPackaging(int mealId, int variantId, int packagingId)
    {
        await _mealService.DeleteMealVariantPackagingAsync(variantId, packagingId);
        TempData["Success"] = "Opakowanie wariantu zostalo usuniete.";
        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishVariant(int mealId, int variantId)
    {
        try
        {
            await _mealService.PublishMealVariantAsync(variantId);
            TempData["Success"] = "Wariant posilku zostal opublikowany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpPost("{mealId:int}/variants/{variantId:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveVariant(int mealId, int variantId)
    {
        try
        {
            await _mealService.ArchiveMealVariantAsync(variantId);
            TempData["Success"] = "Wariant posilku zostal zarchiwizowany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = mealId });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var meal = await _mealService.GetMealWithDetailsAsync(id);
        if (meal == null) return NotFound();
        ViewBag.ComponentVersions = await _recipeComponentService.GetPublishedVersionOptionsAsync();
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
        try
        {
            await _mealService.PublishMealAsync(id);
            TempData["Success"] = "Posilek opublikowany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
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
