using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("production")]
public sealed class ProductionController : Controller
{
    /// <summary>
    /// Renders the production plan index view for the specified date.
    /// </summary>
    /// <param name="date">The date to display on the plan; if null, today's date is used.</param>
    /// <returns>An <see cref="IActionResult"/> that renders the production index view.</returns>
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
    {
        this.ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Plan produkcji", "Produkcja", "Plan dnia z pustym stanem gotowym na komponenty M3.");
        return this.View();
    }

    /// <summary>
    /// Renders the view used to generate a daily production plan for a selected date.
    /// </summary>
    /// <param name="date">Optional production date to generate the plan for; if null, today's date is used.</param>
    /// <returns>The view for generating a daily production plan.</returns>
    [HttpGet("generate")]
    public IActionResult Generate(DateOnly? date)
    {
        this.ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Generuj plan", "Produkcja", "Szkielet generowania dziennego planu produkcji.");
        return this.View();
    }

    /// <summary>
    /// Renders the production day plan view and prepares preview metadata.
    /// </summary>
    /// <param name="planId">Identifier of the production plan to display; use 0 for the default/mock plan.</param>
    /// <returns>The view result for the production plan page.</returns>
    [HttpGet("plan")]
    [HttpGet("plan/{planId:int}")]
    public IActionResult Plan(int planId = 0)
    {
        this.ViewBag.PlanId = planId;
        this.SetPreview("Plan dnia", "Produkcja", "Podgląd planu produkcyjnego w trybie mock.");
        return this.View();
    }

    /// <summary>
    /// Renders the preview list view for cooking cards in the production section.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that renders the cooking cards view.</returns>
    [HttpGet("cooking-cards")]
    public IActionResult CookingCards()
    {
        this.SetPreview("Karty gotowania", "Produkcja", "Lista kart gotowania do realizacji w wersji preview.");
        return this.View();
    }

    /// <summary>
    /// Renders the cooking card preview view for a specific plan item.
    /// </summary>
    /// <param name="planItemId">Identifier of the plan item to preview; 0 indicates no specific item.</param>
    /// <returns>An IActionResult that renders the cooking card preview view.</returns>
    [HttpGet("cooking-card")]
    [HttpGet("cooking-card/{planItemId:int}")]
    public IActionResult CookingCard(int planItemId = 0)
    {
        this.ViewBag.PlanItemId = planItemId;
        this.SetPreview("Karta gotowania", "Produkcja", "Podgląd pojedynczej karty gotowania bez zależności od usług M3.");
        return this.View();
    }

    /// <summary>
    /// Populate view preview metadata into ViewData for rendering page title, section and description.
    /// </summary>
    /// <param name="title">The preview title to display (ViewData["Title"]).</param>
    /// <param name="section">The preview section name to display (ViewData["Section"]).</param>
    /// <param name="description">The preview description to display (ViewData["Description"]).</param>
    private void SetPreview(string title, string section, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = section;
        this.ViewData["Description"] = description;
    }
}
