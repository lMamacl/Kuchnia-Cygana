using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("admin")]
public sealed class AdminController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Admin";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Dashboard administracyjny systemu.";
        return View();
    }

    [HttpGet("users")]
    public IActionResult Users()
    {
        ViewData["Title"] = "Uzytkownicy";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie uzytkownikami.";
        return View();
    }

    [HttpGet("roles")]
    public IActionResult Roles()
    {
        ViewData["Title"] = "Role";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie rolami i uprawnieniami.";
        return View();
    }

    [HttpGet("settings")]
    public IActionResult Settings()
    {
        ViewData["Title"] = "Ustawienia";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Ustawienia systemowe.";
        return View();
    }
}

