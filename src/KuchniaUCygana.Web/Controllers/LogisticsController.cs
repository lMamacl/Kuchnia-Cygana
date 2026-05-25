using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Driver,Admin")]
[Route("logistics")]
public sealed class LogisticsController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Logistyka";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Dashboard dzisiejszych tras i gotowosci zaladunku.";
        return View();
    }

    [HttpGet("routes")]
    public IActionResult Routes()
    {
        ViewData["Title"] = "Trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista tras z filtrem dnia.";
        return View();
    }

    [HttpGet("routes/create")]
    public IActionResult CreateRoute()
    {
        ViewData["Title"] = "Nowa trasa";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Szkielet kreatora trasy.";
        return View();
    }

    [HttpGet("routes/{id:int?}")]
    public IActionResult RouteDetails(int? id)
    {
        ViewData["Title"] = "Edycja trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder trasy #{id}." : "Placeholder edycji trasy.";
        return View();
    }

    [HttpGet("routes/{id:int}/map")]
    [HttpGet("routes/map")]
    public IActionResult RouteMap(int? id)
    {
        ViewData["Title"] = "Mapa tras";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder mapy trasy #{id}." : "Placeholder mapy tras.";
        return View();
    }

    [HttpGet("vehicles")]
    public IActionResult Vehicles()
    {
        ViewData["Title"] = "Flota";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista pojazdow i gotowosc floty.";
        return View();
    }

    [HttpGet("drivers")]
    public IActionResult Drivers()
    {
        ViewData["Title"] = "Kierowcy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista kierowcow i przypisania do tras.";
        return View();
    }
}
