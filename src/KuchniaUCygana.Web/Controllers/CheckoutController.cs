using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
public sealed class CheckoutController : Controller
{
    private const string CartSessionKey = "cart";

    private readonly IOrderService orderService;
    private readonly ICheckoutService checkoutService;
    private readonly IAddressService addressService;
    private readonly IDiscountService discountService;
    private readonly ICartService cartService;
    private readonly IDeliveryWindowRepository deliveryWindowRepository;
    private readonly IPaymentRepository paymentRepository;
    private readonly IOrderRepository orderRepository;
    private readonly IConfiguration configuration;
    private readonly IMapper mapper;
    private readonly ILogger<CheckoutController> logger;

    public CheckoutController(
        IOrderService orderService,
        ICheckoutService checkoutService,
        IAddressService addressService,
        IDiscountService discountService,
        ICartService cartService,
        IDeliveryWindowRepository deliveryWindowRepository,
        IPaymentRepository paymentRepository,
        IOrderRepository orderRepository,
        IConfiguration configuration,
        IMapper mapper,
        ILogger<CheckoutController> logger)
    {
        this.orderService = orderService;
        this.checkoutService = checkoutService;
        this.addressService = addressService;
        this.discountService = discountService;
        this.cartService = cartService;
        this.deliveryWindowRepository = deliveryWindowRepository;
        this.paymentRepository = paymentRepository;
        this.orderRepository = orderRepository;
        this.configuration = configuration;
        this.mapper = mapper;
        this.logger = logger;
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

        int orderId;
        try
        {
            orderId = await orderService.CreateOrderAsync(request, userId);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Order checkout failed before payment initialization for user {UserId}.", userId);
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Cart = cart;
            model.Addresses = await addressService.GetByUserIdAsync(userId);
            model.Windows = mapper.Map<IEnumerable<DeliveryWindowDto>>(
                await deliveryWindowRepository.GetActiveWindowsAsync());
            return View(model);
        }

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

    [HttpGet]
    public async Task<IActionResult> ProcessPaymentResult(
        string? payment_intent,
        string? payment_intent_client_secret,
        string? redirect_status)
    {
        // Stripe redirectuje na return_url metoda GET i dopina payment_intent w query stringu.
        if (string.IsNullOrWhiteSpace(payment_intent))
        {
            TempData["ErrorMessage"] = "Stripe nie zwrocil identyfikatora platnosci.";
            return RedirectToAction("Index", "Order");
        }

        var payment = await paymentRepository.GetByStripeIntentIdAsync(payment_intent);
        if (payment is null)
        {
            TempData["ErrorMessage"] = "Nie znaleziono platnosci w systemie.";
            return RedirectToAction("Index", "Order");
        }

        var order = await orderRepository.GetByIdAsync(payment.OrderId);
        if (order is null || order.CustomerId != GetCurrentUserId())
        {
            return NotFound();
        }

        var isSuccess = await checkoutService.ConfirmPaymentAsync(payment_intent);
        if (isSuccess)
        {
            return RedirectToAction("Success", new { orderId = payment.OrderId });
        }

        var failureMessage = string.Equals(redirect_status, "failed", StringComparison.OrdinalIgnoreCase)
            ? "Platnosc zostala odrzucona przez operatora."
            : "Platnosc nie zostala sfinalizowana.";

        var canRetry = await checkoutService.HandlePaymentFailureAsync(payment_intent, failureMessage);
        if (canRetry)
        {
            TempData["ErrorMessage"] = "Platnosc sie nie powiodla lub zostala przerwana. Sprobuj ponownie.";
            return RedirectToAction("Payment", new { orderId = payment.OrderId });
        }

        TempData["ErrorMessage"] = "Zamowienie zostalo automatycznie anulowane po 3 nieudanych probach platnosci.";
        return RedirectToAction("Details", "Order", new { orderId = payment.OrderId });
    }

    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> Payment(int orderId)
    {
        return await BuildPaymentViewAsync(orderId);
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var stripeSignature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        var webhookSecret = GetStripeWebhookSecret();

        if (string.IsNullOrWhiteSpace(webhookSecret) || string.IsNullOrWhiteSpace(stripeSignature))
        {
            return BadRequest("Brak konfiguracji Stripe WebhookSecret albo naglowka Stripe-Signature.");
        }

        try
        {
            // Bezpieczna weryfikacja podpisu pochodzacego ze Stripe
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent is not null)
                {
                    await checkoutService.ConfirmPaymentAsync(paymentIntent.Id);
                }
            }
            else if (stripeEvent.Type == "payment_intent.payment_failed")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent is not null)
                {
                    var errorMsg = paymentIntent.LastPaymentError?.Message ?? "Blad platnosci zarejestrowany przez webhook.";
                    await checkoutService.HandlePaymentFailureAsync(paymentIntent.Id, errorMsg);
                }
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest($"Blad webhooka: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Success(int orderId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != GetCurrentUserId())
        {
            return NotFound();
        }

        // Po udanym procesie czyscimy koszyk z sesji klienta B2C
        HttpContext.Session.Remove(CartSessionKey);

        return View(order);
    }

    private async Task<IActionResult> BuildPaymentViewAsync(int orderId)
    {
        var userId = GetCurrentUserId();
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != userId)
        {
            return NotFound("Zamowienie nie istnieje.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return RedirectToAction("Success", new { orderId });
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            TempData["Error"] = "Nie mozna oplacic anulowanego zamowienia.";
            return RedirectToAction("Details", "Order", new { orderId });
        }

        try
        {
            var publishableKey = GetStripePublishableKey();
            if (string.IsNullOrWhiteSpace(publishableKey))
            {
                throw new InvalidOperationException("Brakuje konfiguracji Stripe:PublishableKey.");
            }

            // Zapis w DB jako Pending, status zamowienia = PendingPayment
            var clientSecret = await checkoutService.InitiatePaymentAsync(orderId, userId);

            var vm = new PaymentViewModel
            {
                OrderId = orderId,
                OrderNumber = order.OrderNumber,
                FinalPrice = order.FinalPrice,
                StripeClientSecret = clientSecret,
                StripePublishableKey = publishableKey,
            };

            return View("Payment", vm);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize payment for order {OrderId}.", orderId);
            TempData["ErrorMessage"] = $"Nie udalo sie zainicjowac platnosci: {ex.Message}";
            return RedirectToAction("Summary", new { orderId });
        }
    }

    private string GetStripePublishableKey()
    {
        return configuration["Stripe:PublishableKey"]
            ?? Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE")
            ?? string.Empty;
    }

    private string GetStripeWebhookSecret()
    {
        return configuration["Stripe:WebhookSecret"]
            ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
            ?? string.Empty;
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        return int.Parse(value);
    }
}
