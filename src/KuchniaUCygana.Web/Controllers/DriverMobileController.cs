using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("driver")]
public sealed class DriverMobileController : Controller
{
    /// <summary>
    /// Displays the mobile-first driver route overview.
    /// </summary>
    /// <returns>The view for the driver's route overview. Sets ViewData["Title"] to "Moja trasa" and ViewData["Description"] to "Lista stopow kierowcy w wersji mobile-first".</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Moja trasa";
        ViewData["Description"] = "Lista stopow kierowcy w wersji mobile-first.";
        return View();
    }

    /// <summary>
    /// Displays the stop details view for a driver and customizes the description based on an optional stop id.
    /// </summary>
    /// <param name="id">Optional stop identifier; when provided the description includes that stop number.</param>
    /// <returns>A view result that renders the stop details page.</returns>
    [HttpGet("stop")]
    [HttpGet("stop/{id:int?}")]
    public IActionResult Stop(int? id)
    {
        ViewData["Title"] = "Szczegoly stopu";
        ViewData["Description"] = id.HasValue ? $"Placeholder stopu #{id}." : "Placeholder aktywnego stopu.";
        return View();
    }

    /// <summary>
    /// Displays the delivery confirmation view, optionally scoped to a specific stop.
    /// </summary>
    /// <param name="id">Optional stop identifier used to personalize the confirmation content; when null the generic delivery confirmation is shown.</param>
    /// <returns>The view that renders the delivery confirmation page.</returns>
    [HttpGet("stop/confirm")]
    [HttpGet("stop/{id:int}/confirm")]
    public IActionResult Confirm(int? id)
    {
        ViewData["Title"] = "Potwierdzenie dostawy";
        ViewData["Description"] = id.HasValue ? $"Placeholder potwierdzenia stopu #{id}." : "Placeholder potwierdzenia dostawy.";
        return View();
    }

    /// <summary>
    /// Displays the problem-reporting view for a delivery stop.
    /// </summary>
    /// <param name="id">Optional stop identifier; when provided, the view description references that stop number.</param>
    /// <returns>An <see cref="IActionResult"/> that renders the problem report view for the current or specified stop.</returns>
    [HttpGet("stop/problem")]
    [HttpGet("stop/{id:int}/problem")]
    public IActionResult Problem(int? id)
    {
        ViewData["Title"] = "Problem z dostawa";
        ViewData["Description"] = id.HasValue ? $"Placeholder problemu stopu #{id}." : "Placeholder zgloszenia problemu.";
        return View();
    }

    /// <summary>
    /// Displays the driver's daily summary view.
    /// </summary>
    /// <returns>The view for the daily summary page; ViewData contains Title = "Podsumowanie dnia" and Description = "Podsumowanie dostaw po zakonczeniu trasy."</returns>
    [HttpGet("summary")]
    public IActionResult Summary()
    {
        ViewData["Title"] = "Podsumowanie dnia";
        ViewData["Description"] = "Podsumowanie dostaw po zakonczeniu trasy.";
        return View();
    }
}

