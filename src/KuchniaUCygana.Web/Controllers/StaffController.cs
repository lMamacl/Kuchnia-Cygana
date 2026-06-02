using KuchniaUCygana.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("staff")]
[Authorize(Roles = AppRoles.StaffAuthorizationRoles)]
public sealed class StaffController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Panel pracowniczy";
        ViewData["Section"] = "Dashboard";
        ViewData["Description"] = "Centralny punkt nawigacji do modulow operacyjnych Sprint 4B.";
        return View();
    }
}

