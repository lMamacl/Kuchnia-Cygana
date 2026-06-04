using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("diet-editor")]
public sealed class DietEditorController : Controller
{
    /// <summary>
    /// Renders the diet editor dashboard view.
    /// </summary>
    /// <returns>The view for the diet editor dashboard with ViewData keys: Title = "Edytor diet", Section = "Diety", and Description = "Dashboard dietetyka z lista diet i statusami publikacji.".</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Edytor diet";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Dashboard dietetyka z lista diet i statusami publikacji.";
        return View();
    }

    /// <summary>
    /// Displays the "create new diet" page and prepares view metadata.
    /// </summary>
    /// <remarks>
    /// Sets ViewData["Title"], ViewData["Section"], and ViewData["Description"] to describe the new-diet editor in Polish.
    /// </remarks>
    /// <returns>An IActionResult that renders the create-diet view.</returns>
    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nowa dieta";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Szkielet kreatora nowej diety.";
        return View();
    }

    /// <summary>
    /// Renders the diet edit/details view and populates ViewData entries for the page.
    /// </summary>
    /// <remarks>
    /// Sets ViewData["Title"] to "Edycja diety", ViewData["Section"] to "Diety" and ViewData["Description"]
    /// to a placeholder message that includes the diet ID when one is provided.
    /// </remarks>
    /// <param name="id">Optional diet identifier; when present the description will include the ID.</param>
    /// <returns>The view for editing or viewing a diet's details.</returns>
    [HttpGet("{id:int?}")]
    public IActionResult Details(int? id)
    {
        ViewData["Title"] = "Edycja diety";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = id.HasValue ? $"Placeholder edycji diety #{id}." : "Placeholder edycji diety.";
        return View();
    }

    /// <summary>
    /// Render the meals page for the diet editor, optionally scoped to a specific diet.
    /// </summary>
    /// <param name="id">Optional diet identifier; when provided the page description includes the diet number.</param>
    /// <returns>A view result that renders the meals page with Title, Section, and Description set (Description includes the diet id when <paramref name="id"/> has a value).</returns>
    [HttpGet("meals")]
    [HttpGet("{id:int}/meals")]
    public IActionResult Meals(int? id)
    {
        ViewData["Title"] = "Posilki w diecie";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = id.HasValue ? $"Placeholder posilkow diety #{id}." : "Placeholder posilkow w diecie.";
        return View();
    }

    /// <summary>
    /// Renders the recipes base view and populates ViewData with the page title, section, and description used to link recipes to the ingredient catalog.
    /// </summary>
    /// <returns>The view for the recipes base page.</returns>
    [HttpGet("recipes")]
    public IActionResult Recipes()
    {
        ViewData["Title"] = "Przepisy";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Baza przepisow do powiazania z katalogiem skladnikow.";
        return View();
    }

    /// <summary>
    /// Renders the recipe editor view for a specific recipe when an identifier is provided, or a generic recipe editor when no identifier is supplied.
    /// </summary>
    /// <param name="id">Optional recipe identifier; when provided the view is prepared for editing that specific recipe.</param>
    /// <returns>An <see cref="IActionResult"/> that renders the recipe editor view.</returns>
    [HttpGet("recipes/{id:int?}")]
    public IActionResult Recipe(int? id)
    {
        ViewData["Title"] = "Edycja przepisu";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = id.HasValue ? $"Placeholder przepisu #{id}." : "Placeholder edycji przepisu.";
        return View();
    }
}
