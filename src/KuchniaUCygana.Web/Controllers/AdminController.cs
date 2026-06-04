using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("admin")]
public sealed class AdminController : Controller
{
    /// <summary>
    /// Displays the administration dashboard and populates ViewData with UI metadata for the view.
    /// </summary>
    /// <returns>The administration dashboard view.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Admin";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Dashboard administracyjny systemu.";
        return View();
    }

    /// <summary>
    /// Displays the administration page for managing users.
    /// </summary>
    /// <returns>The view for the Users administration page.</returns>
    [HttpGet("users")]
    public IActionResult Users()
    {
        ViewData["Title"] = "Uzytkownicy";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie uzytkownikami.";
        return View();
    }

    /// <summary>
    /// Displays the Roles administration page and prepares view metadata.
    /// </summary>
    /// <returns>The view for managing roles and permissions.</returns>
    [HttpGet("roles")]
    public IActionResult Roles()
    {
        ViewData["Title"] = "Role";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie rolami i uprawnieniami.";
        return View();
    }

    /// <summary>
    /// Displays the administration page for system settings.
    /// </summary>
    /// <returns>A view result for the settings page with ViewData keys "Title" ("Ustawienia"), "Section" ("Administracja"), and "Description" ("Ustawienia systemowe.").</returns>
    [HttpGet("settings")]
    public IActionResult Settings()
    {
        ViewData["Title"] = "Ustawienia";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Ustawienia systemowe.";
        return View();
    }
}

