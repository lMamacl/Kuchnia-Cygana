using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Kitchen,KitchenManager,Warehouse,WarehouseManager,Packing,PackingManager,Dietitian,Logistics,LogisticsManager,Driver,DriverManager,HR,HRManager,BOK,BOKManager,Admin")]
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
        return RedirectToAction("Profile", "Account");
    }
}

