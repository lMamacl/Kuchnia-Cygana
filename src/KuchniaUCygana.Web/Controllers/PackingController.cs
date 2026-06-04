using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("packing")]
public sealed class PackingController : Controller
{
    /// <summary>
    /// Displays the packing sessions index view for a specified date or for today when no date is provided.
    /// </summary>
    /// <param name="date">The selected date to view packing sessions for; when null, defaults to today's date.</param>
    /// <returns>The view for listing packing sessions in preview mode.</returns>
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
    {
        this.ViewBag.SelectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        this.SetPreview("Sesje pakowania", "Kompletacja", "Lista sesji pakowania w wersji preview bez usług M3.");
        return this.View();
    }

    /// <summary>
    /// Displays the active packing session view, optionally for a specified session ID.
    /// </summary>
    /// <param name="sessionId">The session identifier to preview; when omitted the route uses a default of 0.</param>
    /// <returns>The view for the active packing session.</returns>
    [HttpGet("session")]
    [HttpGet("session/{sessionId:int}")]
    public IActionResult Session(int sessionId = 0)
    {
        this.ViewBag.SessionId = sessionId;
        this.SetPreview("Aktywna sesja", "Kompletacja", "Podgląd aktywnej sesji pakowania w trybie mock.");
        return this.View();
    }

    /// <summary>
    /// Displays the labels preview view for the packing section.
    /// </summary>
    /// <returns>A view showing labels and QR codes in preview mode.</returns>
    [HttpGet("labels")]
    public IActionResult LabelsIndex()
    {
        this.SetPreview("Etykiety", "Kompletacja", "Etykiety i QR w trybie preview.");
        return this.View();
    }

    /// <summary>
    /// Displays the labels view for the specified packing session in preview mode.
    /// </summary>
    /// <param name="sessionId">Identifier of the packing session whose labels are shown.</param>
    /// <returns>A view populated with preview metadata and a QR label placeholder for the given session.</returns>
    [HttpGet("labels/{sessionId:int}")]
    public IActionResult Labels(int sessionId)
    {
        this.ViewBag.SessionId = sessionId;
        this.SetPreview("Etykiety sesji", "Kompletacja", $"Placeholder etykiet QR dla sesji #{sessionId}.");
        return this.View();
    }

    /// <summary>
    /// Populate view metadata (title, section and description) used by packing preview pages.
    /// </summary>
    /// <param name="title">The page title to display.</param>
    /// <param name="section">The section or category name to display.</param>
    /// <param name="description">A brief description shown on the preview page.</param>
    private void SetPreview(string title, string section, string description)
    {
        this.ViewData["Title"] = title;
        this.ViewData["Section"] = section;
        this.ViewData["Description"] = description;
    }
}
