using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Kitchen,KitchenManager,Warehouse,WarehouseManager,Packing,PackingManager,Dietitian,Logistics,LogisticsManager,Driver,DriverManager,HR,HRManager,BOK,BOKManager,Admin")]
[Route("staff")]
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

