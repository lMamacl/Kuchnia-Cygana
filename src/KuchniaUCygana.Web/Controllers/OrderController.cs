using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        var userId = GetCurrentUserId();
        var orders = await orderService.GetByCustomerIdAsync(userId);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int orderId)
    {
        var userId = GetCurrentUserId();
        var order = await orderService.GetByIdAsync(orderId, userId);

        if (order is null)
            return NotFound();

        return View(order);
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
    public async Task<IActionResult> ChangeDelivery(
        int deliveryCalendarId, DateTime newDate, int? newAddressId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> SkipDelivery(int deliveryCalendarId, string reason)
    {
        throw new NotImplementedException();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Brak identyfikatora użytkownika w tokenie.");
        return int.Parse(value);
    }
}
