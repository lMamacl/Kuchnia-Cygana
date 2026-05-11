using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
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

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        throw new NotImplementedException();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int orderId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int orderId)
    {
        throw new NotImplementedException();
    }

    [HttpGet]
    public async Task<IActionResult> ChangeDelivery(int deliveryCalendarId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> ChangeDelivery(int deliveryCalendarId, DateTime newDate, int? newAddressId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> SkipDelivery(int deliveryCalendarId, string reason)
    {
        throw new NotImplementedException();
    }
}
