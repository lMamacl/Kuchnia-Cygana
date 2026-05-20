using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("menu")]
public sealed class MenuController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Menu";
        ViewData["Description"] = "Publiczny katalog diet i jadlospisow.";
        return View();
    }

    [HttpGet("diet/{id:int?}")]
    public IActionResult Diet(int? id)
    {
        ViewData["Title"] = "Szczegoly diety";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder szczegolow diety #{id}."
            : "Placeholder szczegolow diety bez wybranego identyfikatora.";
        return View();
    }

    [HttpGet("week-plan/{dietId:int?}")]
    public IActionResult WeekPlan(int? dietId)
    {
        ViewData["Title"] = "Jadlospis tygodniowy";
        ViewData["Description"] = dietId.HasValue
            ? $"Placeholder jadlospisu dla diety #{dietId}."
            : "Placeholder tygodniowego jadlospisu.";
        return View();
    }
}

