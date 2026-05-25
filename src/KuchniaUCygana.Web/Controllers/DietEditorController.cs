using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Dietitian,Admin")]
[Route("diet-editor")]
public sealed class DietEditorController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Edytor diet";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Dashboard dietetyka z lista diet i statusami publikacji.";
        return View();
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Nowa dieta";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Szkielet kreatora nowej diety.";
        return View();
    }

    [HttpGet("{id:int?}")]
    public IActionResult Details(int? id)
    {
        ViewData["Title"] = "Edycja diety";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = id.HasValue ? $"Placeholder edycji diety #{id}." : "Placeholder edycji diety.";
        return View();
    }

    [HttpGet("meals")]
    [HttpGet("{id:int}/meals")]
    public IActionResult Meals(int? id)
    {
        ViewData["Title"] = "Posilki w diecie";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = id.HasValue ? $"Placeholder posilkow diety #{id}." : "Placeholder posilkow w diecie.";
        return View();
    }

    [HttpGet("recipes")]
    public IActionResult Recipes()
    {
        ViewData["Title"] = "Przepisy";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = "Baza przepisow do powiazania z katalogiem skladnikow.";
        return View();
    }

    [HttpGet("recipes/{id:int?}")]
    public IActionResult Recipe(int? id)
    {
        ViewData["Title"] = "Edycja przepisu";
        ViewData["Section"] = "Diety";
        ViewData["Description"] = id.HasValue ? $"Placeholder przepisu #{id}." : "Placeholder edycji przepisu.";
        return View();
    }
}
