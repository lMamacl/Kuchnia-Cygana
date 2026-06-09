using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

public sealed class CartController : Controller
{
    private readonly ICartService cartService;
    private readonly IDietOrderingService dietOrderingService;
    private const string CartSessionKey = "cart";

    public CartController(ICartService cartService, IDietOrderingService dietOrderingService)
    {
        this.cartService = cartService;
        this.dietOrderingService = dietOrderingService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var cart = GetSessionCart();
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int dietVariantId, int totalDays)
    {
        CartItemDto item;
        try
        {
            item = await dietOrderingService.CreateCartItemAsync(dietVariantId, totalDays);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            if (Request.Headers.ContainsKey("HX-Request"))
                return BadRequest(ex.Message);

            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Menu");
        }

        var cart = GetSessionCart();
        cart = cartService.AddItem(cart, item);
        SaveSessionCart(cart);

        if (Request.Headers.ContainsKey("HX-Request"))
            return PartialView("_CartItems", cart);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Remove(int dietVariantId)
    {
        var cart = GetSessionCart();
        cart = cartService.RemoveItem(cart, dietVariantId);
        SaveSessionCart(cart);

        if (Request.Headers.ContainsKey("HX-Request"))
            return PartialView("_CartItems", cart);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Clear()
    {
        SaveSessionCart(cartService.ClearCart());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Checkout()
    {
        var cart = GetSessionCart();
        if (!cart.Items.Any())
        {
            TempData["Error"] = "Koszyk jest pusty.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction("Index", "Checkout");
    }

    private CartDto GetSessionCart()
    {
        var serialized = HttpContext.Session.GetString(CartSessionKey);
        return cartService.GetCart(serialized);
    }

    private void SaveSessionCart(CartDto cart)
    {
        HttpContext.Session.SetString(CartSessionKey, cartService.Serialize(cart));
    }
}
