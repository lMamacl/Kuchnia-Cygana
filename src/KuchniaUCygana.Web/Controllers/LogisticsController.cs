using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Web.Models.Logistics;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("logistics")]
public sealed class LogisticsController : Controller
{
    private readonly IVehicleService _vehicleService;
    private readonly IDeliveryRouteService _deliveryRouteService;
    private readonly IGeocodeService _geocodeService;
    private readonly IDeliveryRouteService _deliveryRouteService;

    public LogisticsController(
        IVehicleService vehicleService,
        IGeocodeService geocodeService,
        IDeliveryRouteService deliveryRouteService)
    {
        _vehicleService = vehicleService;
        _deliveryRouteService = deliveryRouteService;
        _geocodeService = geocodeService;
        _deliveryRouteService = deliveryRouteService;
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
    public async Task<IActionResult> Routes(DateTimeOffset? date)
    {
        var selectedDate = date ?? DateTimeOffset.Now;
        ViewData["Title"] = "Trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Lista tras z filtrem dnia.";
        ViewBag.SelectedDate = selectedDate;
        return View(await _deliveryRouteService.GetRoutesForDateAsync(selectedDate));
    }

    [HttpPost("routes/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateRoutes(GenerateDailyRoutesRequest request)
    {
        var result = await _deliveryRouteService.GenerateDailyRoutesAsync(request);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"Wygenerowano {result.GeneratedRoutesCount} tras i {result.PlannedStopsCount} stopow."
            : string.Join(" ", result.Issues.Select(i => i.Message));

        return RedirectToAction(nameof(Routes), new { date = request.RouteDate.ToString("yyyy-MM-dd") });
    }

    [HttpGet("routes/create")]
    public IActionResult CreateRoute()
    {
        ViewData["Title"] = "Nowa trasa";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Generowanie tras na podstawie dostaw z kalendarza.";
        return View(new GenerateDailyRoutesRequest
        {
            RouteDate = DateTimeOffset.Now.Date,
            DefaultDeliveryLoadKg = 1m,
        });
    }

    [HttpGet("routes/{id:int?}")]
    public async Task<IActionResult> RouteDetails(int? id)
    {
        var route = await _deliveryRouteService.GetRouteDetailsAsync(id);
        if (route == null) return NotFound();

        ViewData["Title"] = "Edycja trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue ? $"Placeholder trasy #{id}." : "Placeholder edycji trasy.";
        var route = id.HasValue ? await _deliveryRouteService.GetRouteDetailsAsync(id.Value) : null;
        return View(route);
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
        if (await _vehicleService.GetByRegistrationNumberAsync(request.RegistrationNumber) != null)
        {
            ModelState.AddModelError("RegistrationNumber", "Vehicle with this registration number already exists.");
        }

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
            MaxLoadKg = vehicle.MaxLoadKg,
            Status = Enum.TryParse<VehicleStatus>(vehicle.Status, out var status)
                ? status
                : VehicleStatus.Active,
        };
        return View("VehiclesEdit", updateRequest);
    }

    [HttpPost("vehicles/edit/{id:int}")]
    public async Task<IActionResult> EditVehicle(int id, UpdateVehicleRequest request)
    {
        if (await _vehicleService.GetByRegistrationNumberAsync(request.RegistrationNumber) is VehicleDto existingVehicle &&
            existingVehicle.Id != id)
        {
            ModelState.AddModelError("RegistrationNumber", "Vehicle with this registration number already exists.");
        }

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

    [HttpGet("test-geocode")]
    public IActionResult TestGeokodowania()
    {
        ViewData["Title"] = "Test geokodowania";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Test geokodowania.";
        return View();
    }

    [HttpPost("test-geocode")]
    public async Task<IActionResult> TestGeokodowania(string address)
    {
        var result = await _geocodeService.GeocodeAsync(address);

        // Mapujemy wynik z ValueTuple na typ anonimowy, aby serializator JSON zadziałał prawidłowo
        return Json(new { 
            latitude = result.Latitude, 
            longitude = result.Longitude 
        });
    }
}
