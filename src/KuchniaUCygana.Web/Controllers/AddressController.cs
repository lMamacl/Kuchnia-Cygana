using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[AllowAnonymous]
public sealed class AddressController : Controller
{
    private readonly IAddressService addressService;

    public AddressController(IAddressService addressService)
    {
        this.addressService = addressService;
    }

    [HttpGet("/account/addresses")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Adresy";
        ViewData["Description"] = "Placeholder zarzadzania adresami klienta.";
        return View();
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(CreateAddressRequest request)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Dodawanie adresu jest pominiete w wersji preview.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        ViewData["Title"] = "Edycja adresu";
        ViewData["Description"] = $"Placeholder edycji adresu #{id}.";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UpdateAddressRequest request)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Edycja adresu jest pominieta w wersji preview.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetDefault(int id)
    {
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
    }
}
