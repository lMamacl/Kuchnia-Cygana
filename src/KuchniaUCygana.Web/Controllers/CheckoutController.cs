using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[AllowAnonymous]
[Route("checkout")]
public sealed class CheckoutController : Controller
{
    private readonly ICheckoutService checkoutService;
    private readonly IAddressService addressService;
    private readonly IDiscountService discountService;

    public CheckoutController(
        ICheckoutService checkoutService,
        IAddressService addressService,
        IDiscountService discountService)
    {
        this.checkoutService = checkoutService;
        this.addressService = addressService;
        this.discountService = discountService;
    }

    [HttpGet("")]
    public IActionResult Index(int orderId = 0)
    {
        ViewData["Title"] = "Checkout";
        ViewData["Description"] = "Placeholder finalizacji zamowienia.";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(int orderId, CreateOrderRequest request)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Summary), new { orderId });
    }

    [HttpGet("summary/{orderId:int?}")]
    public IActionResult Summary(int? orderId)
    {
        ViewData["Title"] = "Podsumowanie";
        ViewData["Description"] = orderId.HasValue
            ? $"Placeholder podsumowania zamowienia #{orderId}."
            : "Placeholder podsumowania zamowienia.";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ApplyDiscount(ApplyDiscountRequest request)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmOrder(int orderId)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Payment), new { orderId });
    }

    [HttpGet("payment/{orderId:int?}")]
    public IActionResult Payment(int? orderId)
    {
        ViewData["Title"] = "Platnosc";
        ViewData["Description"] = orderId.HasValue
            ? $"Placeholder platnosci zamowienia #{orderId}."
            : "Placeholder platnosci.";
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StripeWebhook()
    {
        await Task.CompletedTask;
        return Ok();
    }

    [HttpGet("success")]
    public IActionResult Success(int orderId = 0, string? orderNumber = null)
    {
        ViewData["Title"] = "Zamowienie przyjete";
        ViewData["Description"] = string.IsNullOrWhiteSpace(orderNumber)
            ? "Placeholder sukcesu zamowienia."
            : $"Placeholder sukcesu zamowienia {orderNumber}.";
        return View();
    }
}
