using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Kitchen,KitchenManager,Warehouse,WarehouseManager,Packing,PackingManager,Dietitian,Admin")]
[Route("ingredients")]
public sealed class IngredientController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Katalog skladnikow";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Wspolny katalog M2/M3 do wyszukiwania i zarzadzania skladnikami.";
        return View();
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Dodaj skladnik";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Szkielet formularza tworzenia skladnika.";
        return View();
    }

    [HttpGet("{id:int?}")]
    public IActionResult Details(int? id)
    {
        ViewData["Title"] = "Edycja skladnika";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder edycji skladnika #{id}."
            : "Placeholder edycji skladnika bez wybranego identyfikatora.";
        return View();
    }

    [HttpGet("nutrition")]
    [HttpGet("{id:int}/nutrition")]
    public IActionResult Nutrition(int? id)
    {
        ViewData["Title"] = "Wartosci odzywcze";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder wartosci odzywczych skladnika #{id}."
            : "Placeholder wartosci odzywczych skladnika.";
        return View();
    }
}
