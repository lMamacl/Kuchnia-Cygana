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
    private readonly IGeocodeService _geocodeService;
    private readonly IDeliveryRouteService _deliveryRouteService;

    public LogisticsController(
        IVehicleService vehicleService,
        IGeocodeService geocodeService,
        IDeliveryRouteService deliveryRouteService)
    {
        _vehicleService = vehicleService;
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
        return View(new RoutesViewModel
        {
            SelectedDate = selectedDate,
            Routes = await _deliveryRouteService.GetRoutesForDateAsync(selectedDate),
        });
    }

    [HttpPost("routes/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateDailyRoutes(GenerateDailyRoutesRequest request)
    {
        var result = await _deliveryRouteService.GenerateDailyRoutesAsync(request);
        return View("Routes", new RoutesViewModel
        {
            SelectedDate = request.RouteDate,
            Routes = await _deliveryRouteService.GetRoutesForDateAsync(request.RouteDate),
            GenerationResult = result,
        });
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

    [HttpGet("routes/{id:int}")]
    public async Task<IActionResult> RouteDetails(int id)
    {
        var route = await _deliveryRouteService.GetRouteDetailsAsync(id);
        if (route == null) return NotFound();

        ViewData["Title"] = "Szczegoly trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Trasa #{id}: {route.Name}.";
        return View(route);
    }

    [HttpGet("routes/edit/{id:int}")]
    public async Task<IActionResult> EditRoute(int id)
    {
        var route = await _deliveryRouteService.GetRouteDetailsAsync(id);
        if (route == null) return NotFound();

        ViewData["Title"] = "Edycja trasy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Zmiana przypisania i kolejnosci przystankow trasy #{id}.";
        return View("RoutesEdit", await BuildRouteEditViewModelAsync(route));
    }

    [HttpPost("routes/edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoute(int id, RouteEditViewModel model)
    {
        if (id != model.Route.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            var existingRoute = await _deliveryRouteService.GetRouteDetailsAsync(id);
            if (existingRoute == null) return NotFound();
            return View("RoutesEdit", await BuildRouteEditViewModelAsync(existingRoute, model.Route));
        }

        try
        {
            var updated = await _deliveryRouteService.UpdateRouteAsync(model.Route);
            if (updated == null) return NotFound();

            TempData["Success"] = "Trasa zostala zaktualizowana.";
            return RedirectToAction(nameof(RouteDetails), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var existingRoute = await _deliveryRouteService.GetRouteDetailsAsync(id);
            if (existingRoute == null) return NotFound();
            return View("RoutesEdit", await BuildRouteEditViewModelAsync(existingRoute, model.Route));
        }
    }

    [HttpPost("routes/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoute(int id, DateTimeOffset? date)
    {
        try
        {
            var deleted = await _deliveryRouteService.DeleteRouteAsync(id);
            TempData[deleted ? "Success" : "Error"] = deleted
                ? "Trasa zostala usunieta. Mozesz ponownie wygenerowac plan dnia."
                : "Nie znaleziono trasy.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Routes), new { date = date?.ToString("yyyy-MM-dd") });
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

    private async Task<RouteEditViewModel> BuildRouteEditViewModelAsync(
        DeliveryRouteDto route,
        UpdateDeliveryRouteRequest? request = null)
    {
        request ??= new UpdateDeliveryRouteRequest
        {
            Id = route.Id,
            Name = route.Name,
            VehicleId = route.VehicleId ?? 0,
            Stops = route.Stops
                .OrderBy(stop => stop.SequenceNumber)
                .Select(stop => new UpdateDeliveryRouteStopRequest
                {
                    StopId = stop.Id,
                    SequenceNumber = stop.SequenceNumber,
                })
                .ToList(),
        };

        var vehicles = (await _vehicleService.GetAllAsync())
            .Where(vehicle => vehicle.Status == VehicleStatus.Active.ToString())
            .OrderBy(vehicle => vehicle.RegistrationNumber)
            .ToList();

        return new RouteEditViewModel
        {
            Route = request,
            Vehicles = vehicles,
            Stops = route.Stops
                .OrderBy(stop => stop.SequenceNumber)
                .ToList(),
        };
    }
}
