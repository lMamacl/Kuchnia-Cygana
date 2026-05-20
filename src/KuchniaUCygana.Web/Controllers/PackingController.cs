using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("packing")]
public sealed class PackingController : Controller
{
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
    {
        this.ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Sesje pakowania", "Kompletacja", "Lista sesji pakowania w wersji preview bez usług M3.");
        return this.View();
    }

    [HttpGet("session")]
    [HttpGet("session/{sessionId:int}")]
    public IActionResult Session(int sessionId = 0)
    {
        this.ViewBag.SessionId = sessionId;
        this.SetPreview("Aktywna sesja", "Kompletacja", "Podgląd aktywnej sesji pakowania w trybie mock.");
        return this.View();
    }

    [HttpGet("labels")]
    public IActionResult LabelsIndex()
    {
        this.SetPreview("Etykiety", "Kompletacja", "Etykiety i QR w trybie preview.");
        return this.View();
    }

    [HttpGet("labels/{sessionId:int}")]
    public IActionResult Labels(int sessionId)
    {
        this.ViewBag.SessionId = sessionId;
        this.SetPreview("Etykiety sesji", "Kompletacja", $"Placeholder etykiet QR dla sesji #{sessionId}.");
        return this.View();
    }

    private void SetPreview(string title, string section, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = section;
        this.ViewData["Description"] = description;
    }
}
