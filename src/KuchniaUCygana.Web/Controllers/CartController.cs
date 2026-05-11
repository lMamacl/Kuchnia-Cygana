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
        throw new NotImplementedException();
    }

    [HttpPost]
    public IActionResult Add(CartItemDto item)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public IActionResult Remove(int dietVariantId)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public IActionResult Clear()
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> Checkout()
    {
        throw new NotImplementedException();
    }
}
