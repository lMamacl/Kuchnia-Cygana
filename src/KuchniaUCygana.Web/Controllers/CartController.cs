using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

public sealed class CartController : Controller
{
    private readonly ICartService cartService;
    private const string CartSessionKey = "cart";

    public CartController(ICartService cartService)
    {
        this.cartService = cartService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var cart = GetSessionCart();
        return View(cart);
    }

    [HttpPost]
    public IActionResult Add(CartItemDto item)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
