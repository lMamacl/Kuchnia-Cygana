using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("warehouse")]
public sealed class WarehouseController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        this.SetPreview("Magazyn", "Magazyn", "Przegląd stanów i alertów w wersji szkieletowej.");
        return this.View();
    }

    [HttpGet("alerts")]
    public IActionResult Alerts()
    {
        this.SetPreview("Alerty magazynowe", "Magazyn", "Pusty stan alertów magazynowych.");
        return this.PartialView("_AlertsPartial");
    }

    [HttpGet("receive")]
    public IActionResult Receive()
    {
        this.SetPreview("Przyjęcia dostaw", "Magazyn", "Szkielet przyjmowania dostaw bez zapisu do bazy.");
        return this.View();
    }

    [HttpGet("waste")]
    public IActionResult Waste()
    {
        this.SetPreview("Odpady", "Magazyn", "Rejestr odpadów i strat w wersji preview.");
        return this.View();
    }

    [HttpGet("inventory")]
    public IActionResult Inventory()
    {
        this.SetPreview("Inwentaryzacja", "Magazyn", "Ekran inwentaryzacji gotowy do podpięcia usług.");
        return this.View();
    }

    [HttpGet("temperatures")]
    public IActionResult Temperatures()
    {
        this.SetPreview("Temperatury HACCP", "Magazyn", "Monitoring temperatur HACCP w trybie mock.");
        return this.View();
    }

    [HttpGet("haccp-report")]
    public IActionResult HaccpReport(DateOnly? from, DateOnly? to)
    {
        this.ViewBag.DateFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        this.ViewBag.DateTo = to ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Raport HACCP", "Magazyn", "Raport kontrolny HACCP bez generowania danych domenowych.");
        return this.View();
    }

    private void SetPreview(string title, string section, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = section;
        this.ViewData["Description"] = description;
    }
}
