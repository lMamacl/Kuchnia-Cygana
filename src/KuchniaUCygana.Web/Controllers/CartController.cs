using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

public sealed class CartController : Controller
{
    private readonly ICartService cartService;

    public CartController(ICartService cartService)
    {
        this.cartService = cartService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Koszyk";
        ViewData["Description"] = "Placeholder koszyka klienta.";
        return View();
    }

    [HttpPost]
    public IActionResult Add(CartItemDto item)
    {
        TempData["Success"] = "Dodawanie do koszyka jest pominiete w wersji preview.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Remove(int dietVariantId)
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Clear()
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Checkout()
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(CheckoutController.Index), "Checkout");
    }
}
