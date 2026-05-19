using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;

namespace KuchniaUCygana.Web.Controllers;

//[Authorize]
public class VehiclesController : Controller
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    public async Task<IActionResult> Index()
    {
        var vehicles = await _vehicleService.GetAllAsync();
        return View(vehicles);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(CreateVehicleRequest request)
    {
        if (!ModelState.IsValid) return View(request);
        await _vehicleService.CreateAsync(request);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vehicle = await _vehicleService.GetByIdAsync(id);
        if (vehicle == null) return NotFound();
        var updateRequest = new UpdateVehicleRequest
        {
            Id = id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Model = vehicle.Model,
            MaxLoadKg = vehicle.MaxLoadKg
        };
        return View(updateRequest);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UpdateVehicleRequest request)
    {
        if (id != request.Id) return BadRequest();
        if (!ModelState.IsValid) return View(request);
        var updated = await _vehicleService.UpdateAsync(id, request);
        if (updated == null) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _vehicleService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
