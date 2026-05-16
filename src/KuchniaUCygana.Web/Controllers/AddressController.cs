using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
public sealed class AddressController : Controller
{
    private readonly IAddressService addressService;

    public AddressController(IAddressService addressService)
    {
        this.addressService = addressService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var addresses = await addressService.GetByUserIdAsync(userId);
        return View(addresses);
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(CreateAddressRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<IActionResult> Edit(int id)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UpdateAddressRequest request)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<IActionResult> SetDefault(int id)
    {
        throw new NotImplementedException();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Brak identyfikatora użytkownika w tokenie.");
        return int.Parse(value);
    }
}
