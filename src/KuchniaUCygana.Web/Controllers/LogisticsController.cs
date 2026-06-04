using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("logistics")]
public sealed class LogisticsController : Controller
{
    /// <summary>
    /// Renders the logistics dashboard page and sets the page metadata (title, section, description).
    /// </summary>
    /// <returns>The view for the logistics dashboard.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Logistyka";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Dashboard dzisiejszych tras i gotowosci zaladunku.";
        return View();
    }

    /// <summary>
    /// Displays the routes list page and sets page metadata (title, section, description).
    /// </summary>
    /// <returns>A view result that renders the routes list page.</returns>
    [HttpGet("routes")]
    public IActionResult Routes()
    {
        ViewData["Title"] = "Trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista tras z filtrem dnia.";
        return View();
    }

    /// <summary>
    /// Renders the new route creation page and prepares page metadata.
    /// </summary>
    /// <remarks>
    /// Sets ViewData["Title"] to "Nowa trasa", ViewData["Section"] to "Logistyka", and ViewData["Description"] to a placeholder for the route creator.
    /// </remarks>
    /// <returns>The view result that renders the new route creation page.</returns>
    [HttpGet("routes/create")]
    public IActionResult CreateRoute()
    {
        ViewData["Title"] = "Nowa trasa";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Szkielet kreatora trasy.";
        return View();
    }

    /// <summary>
    /// Renders the route details/editor page and sets page metadata in ViewData.
    /// </summary>
    /// <param name="id">Optional route identifier; when provided the description will reference this route number.</param>
    /// <returns>The view for the route details/editor page.</returns>
    [HttpGet("routes/{id:int?}")]
    public IActionResult RouteDetails(int? id)
    {
        ViewData["Title"] = "Edycja trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder trasy #{id}." : "Placeholder edycji trasy.";
        return View();
    }

    /// <summary>
    /// Renders the route map page, optionally scoped to a specific route.
    /// </summary>
    /// <param name="id">Optional route identifier; when provided, the page description includes the route number.</param>
    /// <returns>A view result that renders the route map page.</returns>
    [HttpGet("routes/{id:int}/map")]
    [HttpGet("routes/map")]
    public IActionResult RouteMap(int? id)
    {
        ViewData["Title"] = "Mapa tras";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder mapy trasy #{id}." : "Placeholder mapy tras.";
        return View();
    }

    /// <summary>
    /// Renders the logistics fleet (vehicles) page.
    /// </summary>
    /// <remarks>
    /// Sets ViewData["Title"], ViewData["Section"], and ViewData["Description"] for the fleet page before returning the view.
    /// </remarks>
    /// <returns>The view result for the fleet overview page.</returns>
    [HttpGet("vehicles")]
    public IActionResult Vehicles()
    {
        ViewData["Title"] = "Flota";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista pojazdow i gotowosc floty.";
        return View();
    }

    /// <summary>
    /// Renders the drivers list page and sets page metadata for title, section, and description.
    /// </summary>
    /// <returns>An IActionResult that renders the drivers list view.</returns>
    [HttpGet("drivers")]
    public IActionResult Drivers()
    {
        ViewData["Title"] = "Kierowcy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista kierowcow i przypisania do tras.";
        return View();
    }
}
