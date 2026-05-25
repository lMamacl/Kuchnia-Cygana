using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("driver")]
public sealed class DriverMobileController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Moja trasa";
        ViewData["Description"] = "Lista stopow kierowcy w wersji mobile-first.";
        return View();
    }

    [HttpGet("stop")]
    [HttpGet("stop/{id:int?}")]
    public IActionResult Stop(int? id)
    {
        ViewData["Title"] = "Szczegoly stopu";
        ViewData["Description"] = id.HasValue ? $"Placeholder stopu #{id}." : "Placeholder aktywnego stopu.";
        return View();
    }

    [HttpGet("stop/confirm")]
    [HttpGet("stop/{id:int}/confirm")]
    public IActionResult Confirm(int? id)
    {
        ViewData["Title"] = "Potwierdzenie dostawy";
        ViewData["Description"] = id.HasValue ? $"Placeholder potwierdzenia stopu #{id}." : "Placeholder potwierdzenia dostawy.";
        return View();
    }

    [HttpGet("stop/problem")]
    [HttpGet("stop/{id:int}/problem")]
    public IActionResult Problem(int? id)
    {
        ViewData["Title"] = "Problem z dostawa";
        ViewData["Description"] = id.HasValue ? $"Placeholder problemu stopu #{id}." : "Placeholder zgloszenia problemu.";
        return View();
    }

    [HttpGet("summary")]
    public IActionResult Summary()
    {
        ViewData["Title"] = "Podsumowanie dnia";
        ViewData["Description"] = "Podsumowanie dostaw po zakonczeniu trasy.";
        return View();
    }
}

