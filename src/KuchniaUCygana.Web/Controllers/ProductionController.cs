using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("production")]
public sealed class ProductionController : Controller
{
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
    {
        this.ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Plan produkcji", "Produkcja", "Plan dnia z pustym stanem gotowym na komponenty M3.");
        return this.View();
    }

    [HttpGet("generate")]
    public IActionResult Generate(DateOnly? date)
    {
        this.ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Generuj plan", "Produkcja", "Szkielet generowania dziennego planu produkcji.");
        return this.View();
    }

    [HttpGet("plan")]
    [HttpGet("plan/{planId:int}")]
    public IActionResult Plan(int planId = 0)
    {
        this.ViewBag.PlanId = planId;
        this.SetPreview("Plan dnia", "Produkcja", "Podgląd planu produkcyjnego w trybie mock.");
        return this.View();
    }

    [HttpGet("cooking-cards")]
    public IActionResult CookingCards()
    {
        this.SetPreview("Karty gotowania", "Produkcja", "Lista kart gotowania do realizacji w wersji preview.");
        return this.View();
    }

    [HttpGet("cooking-card")]
    [HttpGet("cooking-card/{planItemId:int}")]
    public IActionResult CookingCard(int planItemId = 0)
    {
        this.ViewBag.PlanItemId = planItemId;
        this.SetPreview("Karta gotowania", "Produkcja", "Podgląd pojedynczej karty gotowania bez zależności od usług M3.");
        return this.View();
    }

    private void SetPreview(string title, string section, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = section;
        this.ViewData["Description"] = description;
    }
}
