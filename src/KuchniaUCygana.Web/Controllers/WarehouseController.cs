using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("warehouse")]
public sealed class WarehouseController : Controller
{
    /// <summary>
    /// Displays the warehouse overview page and prepares preview metadata for the view.
    /// </summary>
    /// <returns>The view result for the warehouse overview (warehouse dashboard) page.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        this.SetPreview("Magazyn", "Magazyn", "Przegląd stanów i alertów w wersji szkieletowej.");
        return this.View();
    }

    /// <summary>
    /// Renders the warehouse alerts partial view and sets preview metadata for the page.
    /// </summary>
    /// <returns>A PartialViewResult that renders the "_AlertsPartial" partial view.</returns>
    [HttpGet("alerts")]
    public IActionResult Alerts()
    {
        this.SetPreview("Alerty magazynowe", "Magazyn", "Pusty stan alertów magazynowych.");
        return this.PartialView("_AlertsPartial");
    }

    /// <summary>
    /// Renders the incoming deliveries page and sets preview metadata for the view.
    /// </summary>
    /// <returns>A view result that renders the receive deliveries page.</returns>
    [HttpGet("receive")]
    public IActionResult Receive()
    {
        this.SetPreview("Przyjęcia dostaw", "Magazyn", "Szkielet przyjmowania dostaw bez zapisu do bazy.");
        return this.View();
    }

    /// <summary>
    /// Displays the warehouse waste register view and sets preview metadata for the page.
    /// </summary>
    /// <returns>The view for the waste register.</returns>
    [HttpGet("waste")]
    public IActionResult Waste()
    {
        this.SetPreview("Odpady", "Magazyn", "Rejestr odpadów i strat w wersji preview.");
        return this.View();
    }

    /// <summary>
    /// Displays the inventory screen for the warehouse.
    /// </summary>
    /// <remarks>
    /// Populates view preview metadata (title, section, description) used by the rendered view.
    /// </remarks>
    /// <returns>The view result for the inventory screen.</returns>
    [HttpGet("inventory")]
    public IActionResult Inventory()
    {
        this.SetPreview("Inwentaryzacja", "Magazyn", "Ekran inwentaryzacji gotowy do podpięcia usług.");
        return this.View();
    }

    /// <summary>
    /// Displays the Temperatures HACCP page and sets preview metadata for mock HACCP monitoring.
    /// </summary>
    /// <returns>A view rendering of the Temperatures HACCP page.</returns>
    [HttpGet("temperatures")]
    public IActionResult Temperatures()
    {
        this.SetPreview("Temperatury HACCP", "Magazyn", "Monitoring temperatur HACCP w trybie mock.");
        return this.View();
    }

    /// <summary>
    /// Renders the HACCP report view with an applied date range.
    /// </summary>
    /// <param name="from">Optional start date for the report range; defaults to seven days before today when null.</param>
    /// <param name="to">Optional end date for the report range; defaults to today when null.</param>
    /// <returns>The view that displays the HACCP report with ViewBag.DateFrom and ViewBag.DateTo set.</returns>
    [HttpGet("haccp-report")]
    public IActionResult HaccpReport(DateOnly? from, DateOnly? to)
    {
        this.ViewBag.DateFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        this.ViewBag.DateTo = to ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Raport HACCP", "Magazyn", "Raport kontrolny HACCP bez generowania danych domenowych.");
        return this.View();
    }

    /// <summary>
    /// Populates view metadata used by views and partials for previewing the page (title, section and description).
    /// </summary>
    /// <param name="title">Page title shown in the view preview.</param>
    /// <param name="section">Section label shown in the view preview.</param>
    /// <param name="description">Short descriptive text shown in the view preview.</param>
    private void SetPreview(string title, string section, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = section;
        this.ViewData["Description"] = description;
    }
}
