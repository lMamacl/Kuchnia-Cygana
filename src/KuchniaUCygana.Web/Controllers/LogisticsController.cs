using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Controllers;

[Route("logistics")]
public sealed class LogisticsController : Controller
{
    private readonly IVehicleService _vehicleService;

    public LogisticsController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Logistyka";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Dashboard dzisiejszych tras i gotowosci zaladunku.";
        return View();
    }

    [HttpGet("routes")]
    public IActionResult Routes()
    {
        ViewData["Title"] = "Trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista tras z filtrem dnia.";
        return View();
    }

    [HttpGet("routes/create")]
    public IActionResult CreateRoute()
    {
        ViewData["Title"] = "Nowa trasa";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Szkielet kreatora trasy.";
        return View();
    }

    [HttpGet("routes/{id:int?}")]
    public IActionResult RouteDetails(int? id)
    {
        ViewData["Title"] = "Edycja trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder trasy #{id}." : "Placeholder edycji trasy.";
        return View();
    }

    [HttpGet("routes/{id:int}/map")]
    [HttpGet("routes/map")]
    public IActionResult RouteMap(int? id)
    {
        ViewData["Title"] = "Mapa tras";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder mapy trasy #{id}." : "Placeholder mapy tras.";
        return View();
    }

    [HttpGet("vehicles")]
    public async Task<IActionResult> Vehicles()
    {
        ViewData["Title"] = "Flota";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Zarządzanie flotą pojazdów dostawczych i ich gotowością operacyjną.";
        var vehicles = await _vehicleService.GetAllAsync();
        return View(vehicles);
    }

    [HttpGet("vehicles/create")]
    public IActionResult CreateVehicle()
    {
        ViewData["Title"] = "Nowy pojazd";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Dodaj nowy pojazd do floty dostawczej.";
        return View("VehiclesCreate");
    }

    [HttpPost("vehicles/create")]
    public async Task<IActionResult> CreateVehicle(CreateVehicleRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Nowy pojazd";
            ViewData["Section"] = "Logistyka";
            ViewData["Description"] = "Dodaj nowy pojazd do floty dostawczej.";
            return View("VehiclesCreate", request);
        }
        await _vehicleService.CreateAsync(request);
        return RedirectToAction(nameof(Vehicles));
    }

    [HttpGet("vehicles/edit/{id:int}")]
    public async Task<IActionResult> EditVehicle(int id)
    {
        var vehicle = await _vehicleService.GetByIdAsync(id);
        if (vehicle == null) return NotFound();

        ViewData["Title"] = "Edycja pojazdu";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Modyfikacja danych pojazdu o rejestracji {vehicle.RegistrationNumber}.";

        var updateRequest = new UpdateVehicleRequest
        {
            Id = id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Model = vehicle.Model,
            MaxLoadKg = vehicle.MaxLoadKg
        };
        return View("VehiclesEdit", updateRequest);
    }

    [HttpPost("vehicles/edit/{id:int}")]
    public async Task<IActionResult> EditVehicle(int id, UpdateVehicleRequest request)
    {
        if (id != request.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edycja pojazdu";
            ViewData["Section"] = "Logistyka";
            ViewData["Description"] = $"Modyfikacja danych pojazdu.";
            return View("VehiclesEdit", request);
        }
        var updated = await _vehicleService.UpdateAsync(id, request);
        if (updated == null) return NotFound();
        return RedirectToAction(nameof(Vehicles));
    }

    [HttpPost("vehicles/delete/{id:int}")]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        await _vehicleService.DeleteAsync(id);
        return RedirectToAction(nameof(Vehicles));
    }

    [HttpGet("drivers")]
    public IActionResult Drivers()
    {
        ViewData["Title"] = "Kierowcy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista kierowcow i przypisania do tras.";
        return View();
    }
}
