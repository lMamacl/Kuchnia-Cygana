using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Dietitian,Admin")]
[Route("diet-editor/recipe-components")]
public sealed class RecipeComponentsController : Controller
{
    private readonly IRecipeComponentManagementService recipeComponentService;
    private readonly IIngredientManagementService ingredientService;
    private readonly ICategoryService categoryService;
    private readonly IWarehouseCategoryService warehouseCategoryService;

    public RecipeComponentsController(
        IRecipeComponentManagementService recipeComponentService,
        IIngredientManagementService ingredientService,
        ICategoryService categoryService,
        IWarehouseCategoryService warehouseCategoryService)
    {
        this.recipeComponentService = recipeComponentService;
        this.ingredientService = ingredientService;
        this.categoryService = categoryService;
        this.warehouseCategoryService = warehouseCategoryService;
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRecipeComponentRequest request)
    {
        try
        {
            var id = await this.recipeComponentService.CreateComponentAsync(request);
            TempData["Success"] = "Skladowa zostala utworzona.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Recipes", "DietEditor");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var component = await this.recipeComponentService.GetComponentAsync(id);
        if (component is null)
        {
            return NotFound();
        }

        ViewData["Title"] = component.Name;
        ViewData["Section"] = "Diety";
        return View("~/Views/DietEditor/RecipeComponentDetails.cshtml", component);
    }

    [HttpPost("{componentId:int}/versions/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVersion(int componentId, CreateRecipeComponentVersionRequest request)
    {
        request.RecipeComponentId = componentId;
        try
        {
            var versionId = await this.recipeComponentService.CreateVersionAsync(request);
            TempData["Success"] = "Utworzono robocza wersje skladowej.";
            return RedirectToAction(nameof(Version), new { componentId, versionId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = componentId });
        }
    }

    [HttpGet("{componentId:int}/versions/{versionId:int}")]
    public async Task<IActionResult> Version(int componentId, int versionId)
    {
        var version = await this.recipeComponentService.GetVersionAsync(versionId);
        if (version is null || version.RecipeComponentId != componentId)
        {
            return NotFound();
        }

        await this.LoadVersionLookupsAsync();
        ViewData["Title"] = $"{version.ComponentName} v{version.VersionNumber}";
        ViewData["Section"] = "Diety";
        return View("~/Views/DietEditor/RecipeComponentVersion.cshtml", version);
    }

    [HttpGet("lookups/ingredients")]
    public async Task<IActionResult> IngredientLookup(string? term, int page = 1)
    {
        var result = await this.ingredientService.SearchAsync(new IngredientSearchFilterDto
        {
            Search = term,
            IsActive = true,
            Page = page <= 0 ? 1 : page,
            PageSize = 20,
        });

        return Json(result.Items
            .Where(item => item.ResourceType is "Food" or "Spice")
            .Select(item => new
            {
                id = item.Id,
                text = item.Name,
                unit = item.Unit,
                stockItemId = item.StockItemId,
                warehouseCategoryId = item.WarehouseCategoryId,
                warehouseCategoryName = item.WarehouseCategoryName,
            }));
    }

    [HttpGet("lookups/categories")]
    public async Task<IActionResult> CategoryLookup(string? term)
    {
        var categories = await this.categoryService.GetAllAsync();
        var normalized = term?.Trim();
        return Json(categories
            .Where(category => string.IsNullOrWhiteSpace(normalized)
                || category.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(category => category.Name)
            .Take(20)
            .Select(category => new
            {
                id = category.Id,
                text = category.Name,
            }));
    }

    [HttpGet("lookups/warehouse-categories")]
    public async Task<IActionResult> WarehouseCategoryLookup(string? term)
    {
        var categories = await this.warehouseCategoryService.GetActiveAsync();
        var normalized = term?.Trim();
        return Json(categories
            .Where(category => string.IsNullOrWhiteSpace(normalized)
                || category.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || category.Code.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(category => category.Name)
            .Take(20)
            .Select(category => new
            {
                id = category.Id,
                text = category.Name,
            }));
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVersion(
        int componentId,
        int versionId,
        UpdateRecipeComponentVersionRequest request)
    {
        try
        {
            await this.recipeComponentService.UpdateVersionAsync(versionId, request);
            TempData["Success"] = "Wersja skladowej zostala zapisana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishVersion(int componentId, int versionId)
    {
        try
        {
            await this.recipeComponentService.PublishVersionAsync(versionId);
            TempData["Success"] = "Wersja skladowej zostala opublikowana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/ingredients")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIngredient(
        int componentId,
        int versionId,
        SaveComponentIngredientRequest request)
    {
        request.RecipeComponentVersionId = versionId;
        try
        {
            await this.recipeComponentService.SaveIngredientAsync(request);
            TempData["Success"] = "Skladnik zostal zapisany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/ingredients/{ingredientRowId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIngredient(int componentId, int versionId, int ingredientRowId)
    {
        await this.recipeComponentService.DeleteIngredientAsync(ingredientRowId);
        TempData["Success"] = "Skladnik zostal usuniety z wersji roboczej.";
        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/packaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePackaging(
        int componentId,
        int versionId,
        SavePackagingRequirementRequest request)
    {
        request.RecipeComponentVersionId = versionId;
        try
        {
            await this.recipeComponentService.SavePackagingAsync(request);
            TempData["Success"] = "Opakowanie zostalo zapisane.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/packaging/{packagingId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePackaging(int componentId, int versionId, int packagingId)
    {
        await this.recipeComponentService.DeletePackagingAsync(packagingId);
        TempData["Success"] = "Opakowanie zostalo usuniete z wersji roboczej.";
        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/instruction-sections")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInstructionSection(
        int componentId,
        int versionId,
        SaveInstructionSectionRequest request)
    {
        request.RecipeComponentVersionId = versionId;
        try
        {
            await this.recipeComponentService.SaveInstructionSectionAsync(request);
            TempData["Success"] = "Sekcja instrukcji zostala zapisana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/instruction-sections/{sectionId:int}/steps")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInstructionStep(
        int componentId,
        int versionId,
        int sectionId,
        SaveInstructionStepRequest request)
    {
        request.RecipeComponentInstructionSectionId = sectionId;
        try
        {
            await this.recipeComponentService.SaveInstructionStepAsync(request);
            TempData["Success"] = "Krok instrukcji zostal zapisany.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/instruction-sections/{sectionId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInstructionSection(int componentId, int versionId, int sectionId)
    {
        await this.recipeComponentService.DeleteInstructionSectionAsync(sectionId);
        TempData["Success"] = "Sekcja instrukcji zostala usunieta z wersji roboczej.";
        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("{componentId:int}/versions/{versionId:int}/instruction-steps/{stepId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInstructionStep(int componentId, int versionId, int stepId)
    {
        await this.recipeComponentService.DeleteInstructionStepAsync(stepId);
        TempData["Success"] = "Krok instrukcji zostal usuniety z wersji roboczej.";
        return RedirectToAction(nameof(Version), new { componentId, versionId });
    }

    [HttpPost("attach-to-meal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachToMeal(AttachComponentToMealRequest request)
    {
        try
        {
            await this.recipeComponentService.AttachComponentToMealAsync(request);
            TempData["Success"] = "Skladowa zostala przypieta do posilku.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Details", "Meals", new { id = request.MealId });
    }

    private async Task LoadVersionLookupsAsync()
    {
        await Task.CompletedTask;
    }
}
