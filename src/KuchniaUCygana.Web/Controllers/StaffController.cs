using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("staff")]
public sealed class StaffController : Controller
{
    /// <summary>
    /// Prepares view metadata for the staff dashboard and returns the corresponding view.
    /// </summary>
    /// <remarks>
    /// Sets ViewData keys:
    /// - "Title" to "Panel pracowniczy"
    /// - "Section" to "Dashboard"
    /// - "Description" to "Centralny punkt nawigacji do modulow operacyjnych Sprint 4B."
    /// </remarks>
    /// <returns>The default view for the staff dashboard.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Panel pracowniczy";
        ViewData["Section"] = "Dashboard";
        ViewData["Description"] = "Centralny punkt nawigacji do modulow operacyjnych Sprint 4B.";
        return View();
    }
}

