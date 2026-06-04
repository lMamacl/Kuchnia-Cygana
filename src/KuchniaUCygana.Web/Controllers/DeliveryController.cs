using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[AllowAnonymous]
[Route("delivery")]
public sealed class DeliveryController : Controller
{
    private readonly IPackingBagRepository bagRepository;
    private readonly IPackingSessionRepository sessionRepository;

    public DeliveryController(
        IPackingBagRepository bagRepository,
        IPackingSessionRepository sessionRepository)
    {
        this.bagRepository = bagRepository;
        this.sessionRepository = sessionRepository;
    }

    [HttpGet("verify/{bagCode}")]
    public async Task<IActionResult> Verify(string bagCode)
    {
        var normalizedCode = TransportLabelCodeNormalizer.Normalize(bagCode);
        var bag = await bagRepository.GetByCodeAsync(normalizedCode);
        if (bag is null)
        {
            return NotFound("Nie znaleziono torby transportowej.");
        }

        var session = await sessionRepository.GetWithItemsAsync(bag.PackingSessionId);
        var lines = new[]
        {
            $"Torba: {bag.BagCode}",
            $"Status: {bag.Status}",
            $"ClientPublicId: {session?.ClientPublicId ?? "-"}",
            $"Pudełka: {session?.Items.Count ?? 0}",
        };

        return Content(string.Join(Environment.NewLine, lines), "text/plain");
    }
}
