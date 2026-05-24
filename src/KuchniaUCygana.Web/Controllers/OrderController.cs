using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
public sealed class OrderController : Controller
{
    private readonly IOrderService orderService;
    private readonly IDeliveryCalendarService deliveryCalendarService;
    private readonly IAddressService addressService;

    public OrderController(
        IOrderService orderService,
        IDeliveryCalendarService deliveryCalendarService,
        IAddressService addressService)
    {
        this.orderService = orderService;
        this.deliveryCalendarService = deliveryCalendarService;
        this.addressService = addressService;
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

        if (order is null) return NotFound();
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int orderId)
    {
        var success = await orderService.CancelOrderAsync(orderId, GetCurrentUserId());
        if (success)
            TempData["Success"] = "Zamówienie zostało pomyślnie anulowane.";
        else
            TempData["Error"] = "Nie można anulować tego zamówienia.";

        return RedirectToAction(nameof(Details), new { orderId });
    }

    [HttpGet]
    public async Task<IActionResult> ChangeDelivery(int deliveryCalendarId)
    {
        var entry = await deliveryCalendarService.GetByIdAsync(deliveryCalendarId);
        if (entry is null) return NotFound();

        var userId = GetCurrentUserId();
        var vm = new OrderChangeDeliveryViewModel
        {
            DeliveryCalendarId = entry.Id,
            CurrentDate = entry.DeliveryDate,
            NewDate = entry.DeliveryDate,
            Addresses = await addressService.GetByUserIdAsync(userId)
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeDelivery(OrderChangeDeliveryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Addresses = await addressService.GetByUserIdAsync(GetCurrentUserId());
            return View(model);
        }

        var success = await deliveryCalendarService.RescheduleDeliveryAsync(
            model.DeliveryCalendarId,
            model.NewDate,
            model.NewAddressId,
            GetCurrentUserId());

        if (success)
        {
            TempData["Success"] = "Dostawa została przełożona.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", "Nie można zmienić dostawy (być może minął czas edycji).");
        model.Addresses = await addressService.GetByUserIdAsync(GetCurrentUserId());
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SkipDelivery(int deliveryCalendarId, string reason)
    {
        var success = await deliveryCalendarService.SkipDeliveryAsync(
            deliveryCalendarId,
            GetCurrentUserId(),
            reason ?? "Pominięte przez panel");

        if (!success)
        {
            return BadRequest("Zbyt późno na anulowanie tej dostawy.");
        }

        var updatedEntry = await deliveryCalendarService.GetByIdAsync(deliveryCalendarId);

        if (updatedEntry != null)
        {
            var address = await addressService.GetByIdAsync(updatedEntry.AddressId, GetCurrentUserId());
            if (address != null) updatedEntry.AddressFullLine = address.FullAddress;
            return PartialView("_DeliveryDayRow", updatedEntry);
        }

        return NotFound();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Brak identyfikatora użytkownika w tokenie.");
        return int.Parse(value);
    }
}
