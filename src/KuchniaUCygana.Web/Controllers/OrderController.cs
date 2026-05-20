using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[AllowAnonymous]
[Route("orders")]
public sealed class OrderController : Controller
{
    private readonly IOrderService orderService;
    private readonly IDeliveryCalendarService deliveryCalendarService;

    public OrderController(
        IOrderService orderService,
        IDeliveryCalendarService deliveryCalendarService)
    {
        this.orderService = orderService;
        this.deliveryCalendarService = deliveryCalendarService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Zamowienia";
        ViewData["Description"] = "Placeholder listy zamowien klienta.";
        return View();
    }

    [HttpGet("{orderId:int?}")]
    public IActionResult Details(int? orderId)
    {
        ViewData["Title"] = "Szczegoly zamowienia";
        ViewData["Description"] = orderId.HasValue
            ? $"Placeholder zamowienia #{orderId}."
            : "Placeholder szczegolow zamowienia.";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int orderId)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("change-delivery/{deliveryCalendarId:int?}")]
    public IActionResult ChangeDelivery(int? deliveryCalendarId)
    {
        ViewData["Title"] = "Zmiana dostawy";
        ViewData["Description"] = deliveryCalendarId.HasValue
            ? $"Placeholder zmiany dostawy #{deliveryCalendarId}."
            : "Placeholder zmiany terminu dostawy.";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ChangeDelivery(int deliveryCalendarId, DateTime newDate, int? newAddressId)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SkipDelivery(int deliveryCalendarId, string reason)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
    }
}
