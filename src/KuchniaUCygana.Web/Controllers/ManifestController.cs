using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Packing,PackingManager,Admin,Logistics,LogisticsManager")]
[Route("manifest")]
public sealed class ManifestController : Controller
{
    [HttpGet("")]
    public IActionResult Index(DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return RedirectToAction("Index", "Loading", new { date = selectedDate.ToString("yyyy-MM-dd") });
    }

    [HttpGet("routes/{routeId:int}")]
    public IActionResult Route(int routeId, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        return RedirectToLoadingManifest(routeId, selectedDate);
    }

    [HttpPost("routes/{routeId:int}/generate")]
    [ValidateAntiForgeryToken]
    public IActionResult Generate(int routeId, DateOnly date, string? changeReason)
    {
        return RedirectToLoadingManifest(routeId, date);
    }

    [HttpPost("routes/{routeId:int}/worker-approve")]
    [ValidateAntiForgeryToken]
    public IActionResult WorkerApprove(int routeId, DateOnly date, bool confirmedManifest)
    {
        return RedirectToLoadingManifest(routeId, date);
    }

    [HttpPost("routes/{routeId:int}/supervisor-approve")]
    [Authorize(Roles = "PackingManager,Admin")]
    [ValidateAntiForgeryToken]
    public IActionResult SupervisorApprove(int routeId, DateOnly date, bool confirmedSupervisor)
    {
        return RedirectToLoadingManifest(routeId, date);
    }

    [HttpGet("routes/{routeId:int}/payload")]
    public IActionResult Payload(int routeId, DateOnly date)
    {
        return RedirectToAction(
            "ManifestPayload",
            "Loading",
            new { routeId, date = date.ToString("yyyy-MM-dd") });
    }

    private IActionResult RedirectToLoadingManifest(int routeId, DateOnly date)
    {
        var url = Url.Action("Manifest", "Loading", new { routeId, date = date.ToString("yyyy-MM-dd") })
            ?? $"/loading/{routeId}/manifest?date={date:yyyy-MM-dd}";

        return Redirect(url);
    }
}
