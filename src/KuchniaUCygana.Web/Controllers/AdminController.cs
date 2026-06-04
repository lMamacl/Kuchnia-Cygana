using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController : Controller
{
    private readonly IAuditLogService auditLogService;
    private readonly IUserService userService;

    public AdminController(IAuditLogService auditLogService, IUserService userService)
    {
        this.auditLogService = auditLogService;
        this.userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Admin";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Dashboard administracyjny systemu.";
        return View(await BuildModelAsync());
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

    [HttpGet("logs")]
    public async Task<IActionResult> Logs()
    {
        ViewData["Title"] = "Logi systemowe";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Audyt zmian w systemie.";
        return View(await BuildModelAsync());
    }

    [HttpGet("settings")]
    public IActionResult Settings()
    {
        ViewData["Title"] = "Ustawienia";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Ustawienia systemowe.";
        return View();
    }

    private async Task<AdminDashboardViewModel> BuildModelAsync()
    {
        return new AdminDashboardViewModel
        {
            SystemLogs = (await auditLogService.GetSystemLogsAsync()).Take(100).ToArray(),
            Users = (await userService.GetAllAsync()).ToArray(),
        };
    }
}
