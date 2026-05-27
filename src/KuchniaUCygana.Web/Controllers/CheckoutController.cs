using System.Security.Claims;
using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
public sealed class CheckoutController : Controller
{
    private readonly IOrderService orderService;
    private readonly ICheckoutService checkoutService;
    private readonly IAddressService addressService;
    private readonly IDiscountService discountService;
    private readonly ICartService cartService;
    private readonly IDeliveryWindowRepository deliveryWindowRepository;
    private readonly IMapper mapper;

    private const string CartSessionKey = "cart";

    public CheckoutController(
        IOrderService orderService,
        ICheckoutService checkoutService,
        IAddressService addressService,
        IDiscountService discountService,
        ICartService cartService,
        IDeliveryWindowRepository deliveryWindowRepository,
        IMapper mapper)
    {
        this.orderService = orderService;
        this.checkoutService = checkoutService;
        this.addressService = addressService;
        this.discountService = discountService;
        this.cartService = cartService;
        this.deliveryWindowRepository = deliveryWindowRepository;
        this.mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var cart = cartService.GetCart(HttpContext.Session.GetString(CartSessionKey));

        if (!cart.Items.Any())
            return RedirectToAction("Index", "Cart");

        var vm = new CheckoutIndexViewModel
        {
            Cart = cart,
            Addresses = await addressService.GetByUserIdAsync(userId),
            Windows = mapper.Map<IEnumerable<DeliveryWindowDto>>(
                await deliveryWindowRepository.GetActiveWindowsAsync()),
            StartDate = DateTime.Today.AddDays(1),
        };

        var defaultAddr = vm.Addresses.FirstOrDefault(a => a.IsDefault)
                       ?? vm.Addresses.FirstOrDefault();
        if (defaultAddr is not null)
            vm.SelectedAddressId = defaultAddr.Id;

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Index(CheckoutIndexViewModel model)
    {
        var userId = GetCurrentUserId();
        var cart = cartService.GetCart(HttpContext.Session.GetString(CartSessionKey));

        if (!cart.Items.Any())
            return RedirectToAction("Index", "Cart");

        // Uzupelnij dane do walidacji widoku w razie bledu
        if (!ModelState.IsValid)
        {
            model.Cart = cart;
            model.Addresses = await addressService.GetByUserIdAsync(userId);
            model.Windows = mapper.Map<IEnumerable<DeliveryWindowDto>>(
                await deliveryWindowRepository.GetActiveWindowsAsync());
            return View(model);
        }

        // Zbuduj zadanie z danych koszyka i formularza
        var request = new CreateOrderRequest
        {
            AddressId = model.SelectedAddressId,
            DeliveryWindowId = model.SelectedWindowId,
            StartDate = model.StartDate,
            Notes = model.Notes,
            Items = cart.Items.Select(i => new CreateOrderItemRequest
            {
                DietId = i.DietId,
                DietVariantId = i.DietVariantId,
                DietName = i.DietName,
                VariantName = i.VariantName,
                CaloriesPerDay = i.CaloriesPerDay,
                PricePerDay = i.PricePerDay,
                TotalDays = i.TotalDays,
            }).ToList(),
        };

        var orderId = await orderService.CreateOrderAsync(request, userId);

        // Czyscimy koszyk po utworzeniu zamowienia
        HttpContext.Session.Remove(CartSessionKey);

        return RedirectToAction(nameof(Summary), new { orderId });
    }

    [HttpGet]
    public async Task<IActionResult> Summary(int orderId)
    {
        var userId = GetCurrentUserId();

        try
        {
            var summary = await checkoutService.BuildSummaryAsync(orderId, userId);
            return View(summary);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> ApplyDiscount(ApplyDiscountRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await discountService.ValidateAndApplyAsync(
            request.Code, request.OrderId, userId);

        // Pobierz aktualne ceny zamowienia do partial view
        var orderDto = await orderService.GetByIdAsync(request.OrderId, userId);

        var vm = new DiscountPartialViewModel
        {
            OrderId = request.OrderId,
            TotalPrice = orderDto?.TotalPrice ?? 0m,
            DiscountAmount = result.IsValid ? result.DiscountAmount : (orderDto?.DiscountAmount ?? 0m),
            FinalPrice = result.IsValid ? result.NewFinalPrice : (orderDto?.FinalPrice ?? 0m),
            ErrorMessage = result.IsValid ? null : result.ErrorMessage,
            WasApplied = result.IsValid,
        };

        return PartialView("_DiscountPartial", vm);
    }

    [HttpPost]
    public IActionResult ConfirmOrder(int orderId)
    {
        // integracja Stripe - TODO
        return RedirectToAction("Payment", new { orderId });
    }

    [HttpGet]
    public IActionResult Payment(int orderId)
    {
        // integracja Stripe - TODO
        ViewBag.OrderId = orderId;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult StripeWebhook()
    {
        // integracja Stripe - TODO
        return Ok();
    }

    [HttpGet]
    public IActionResult Success(int orderId, string orderNumber)
    {
        ViewBag.OrderNumber = orderNumber;
        return View();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        return int.Parse(value);
    }
}
