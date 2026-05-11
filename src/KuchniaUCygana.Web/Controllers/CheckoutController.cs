using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
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

    [HttpGet]
    public async Task<IActionResult> Index(int orderId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> Index(int orderId, CreateOrderRequest request)
    {
        throw new NotImplementedException();
    }

    [HttpGet]
    public async Task<IActionResult> Summary(int orderId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> ApplyDiscount(ApplyDiscountRequest request)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmOrder(int orderId)
    {
        throw new NotImplementedException();
    }

    [HttpGet]
    public async Task<IActionResult> Payment(int orderId)
    {
        throw new NotImplementedException();
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StripeWebhook()
    {
        throw new NotImplementedException();
    }

    [HttpGet]
    public IActionResult Success(int orderId, string orderNumber)
    {
        throw new NotImplementedException();
    }
}
