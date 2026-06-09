using System.Security.Claims;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var addresses = await addressService.GetByUserIdAsync(GetCurrentUserId());
        return View(addresses);
    }

    public IActionResult Create() => View(new CreateAddressRequest());

    [HttpPost]
    public async Task<IActionResult> Create(CreateAddressRequest request)
    {
        if (!ModelState.IsValid) return View(request);

        await addressService.CreateAsync(request, GetCurrentUserId());
        TempData["Success"] = "Adres zostal dodany.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var address = await addressService.GetByIdAsync(id, GetCurrentUserId());
        if (address is null) return NotFound();

        var request = new UpdateAddressRequest
        {
            Id = address.Id,
            Label = address.Label,
            Street = address.Street,
            BuildingNumber = address.BuildingNumber,
            ApartmentNumber = address.ApartmentNumber,
            City = address.City,
            PostalCode = address.PostalCode,
            DeliveryNotes = address.DeliveryNotes,
        };

        return View(request);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UpdateAddressRequest request)
    {
        if (!ModelState.IsValid) return View(request);

        var updated = await addressService.UpdateAsync(request, GetCurrentUserId());
        if (!updated) return NotFound();

        TempData["Success"] = "Adres zostal zaktualizowany.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await addressService.DeleteAsync(id, GetCurrentUserId());
        TempData["Success"] = "Adres zostal usuniety.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetDefault(int id)
    {
        var result = await addressService.SetDefaultAsync(id, GetCurrentUserId());
        if (!result) return NotFound();

        TempData["Success"] = "Domyslny adres zostal zmieniony.";
        return RedirectToAction(nameof(Index));
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        return int.Parse(value);
    }
}
