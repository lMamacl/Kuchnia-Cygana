using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
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
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 10,
        OrderStatus? status = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? orderNumber = null)
    {
        var userId = GetCurrentUserId();
        var result = await orderService.SearchByCustomerAsync(userId, new OrderHistoryQueryDto
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo,
            OrderNumber = orderNumber,
        });

        return View(new OrderHistoryViewModel
        {
            Page = result,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo,
            OrderNumber = orderNumber,
            PageSize = result.PageSize,
        });
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
            TempData["Success"] = "Zamowienie zostalo pomyslnie anulowane.";
        else
            TempData["Error"] = "Nie mozna anulowac tego zamowienia.";

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
            TempData["Success"] = "Dostawa zostala przelozona.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", "Nie mozna zmienic dostawy (byc moze minal czas edycji).");
        model.Addresses = await addressService.GetByUserIdAsync(GetCurrentUserId());
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SkipDelivery(int deliveryCalendarId, string reason)
    {
        var success = await deliveryCalendarService.SkipDeliveryAsync(
            deliveryCalendarId,
            GetCurrentUserId(),
            reason ?? "Pominiete przez panel");

        if (!success)
        {
            return BadRequest("Zbyt pozno na anulowanie tej dostawy.");
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
            ?? throw new UnauthorizedAccessException("Brak identyfikatora uzytkownika w tokenie.");
        return int.Parse(value);
    }
}
