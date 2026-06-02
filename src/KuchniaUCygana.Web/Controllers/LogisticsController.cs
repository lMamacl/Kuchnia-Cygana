using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Web.Models.Logistics;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("logistics")]
public sealed class LogisticsController : Controller
{
    private readonly IVehicleService _vehicleService;
    private readonly IDriverService _driverService;
    private readonly IGeocodeService _geocodeService;
    private readonly GeocodingOrchestrator _geocodingOrchestrator;
    private readonly ILogisticsDeliveryDataProvider _deliveryDataProvider;
    private readonly IAddressRepository _addressRepository;
    private readonly IDeliveryRouteService _deliveryRouteService;

    public LogisticsController(
        IVehicleService vehicleService,
        IDriverService driverService,
        IGeocodeService geocodeService,
        GeocodingOrchestrator geocodingOrchestrator,
        ILogisticsDeliveryDataProvider deliveryDataProvider,
        IAddressRepository addressRepository,
        IDeliveryRouteService deliveryRouteService)
    {
        _vehicleService = vehicleService;
        _driverService = driverService;
        _geocodeService = geocodeService;
        _geocodingOrchestrator = geocodingOrchestrator;
        _deliveryDataProvider = deliveryDataProvider;
        _addressRepository = addressRepository;
        _deliveryRouteService = deliveryRouteService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateTimeOffset? date)
    {
        var selectedDate = date ?? DateTimeOffset.Now;
        ViewData["Title"] = "Logistyka";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Gotowość dostaw na {selectedDate:dd.MM.yyyy}.";
        return View(await BuildDashboardViewModelAsync(selectedDate));
    }

    [HttpPost("geocode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeocodePendingAddresses(
        DateTimeOffset? date,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? DateTimeOffset.Now;
        var pendingBefore = (await _addressRepository.GetPendingAddressesAsync()).Count();

        if (pendingBefore == 0)
        {
            TempData["Success"] = "Wszystkie adresy mają już współrzędne.";
            return RedirectToAction(nameof(Index), new { date = selectedDate.ToString("yyyy-MM-dd") });
        }

        try
        {
            var processed = await _geocodingOrchestrator.ProcessPendingAddressesAsync(cancellationToken);
            var remaining = (await _addressRepository.GetPendingAddressesAsync()).Count();

            TempData[remaining == 0 ? "Success" : "Error"] = remaining == 0
                ? $"Geokodowanie zakończone. Uzupełniono {processed} adresów."
                : $"Uzupełniono {processed} z {pendingBefore} adresów. Nadal oczekuje: {remaining}.";
        }
        catch (Exception)
        {
            TempData["Error"] = "Nie udało się uruchomić geokodowania. Spróbuj ponownie za chwilę.";
        }

        return RedirectToAction(nameof(Index), new { date = selectedDate.ToString("yyyy-MM-dd") });
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

        ViewData["Title"] = "Szczegóły trasy";
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
        ViewData["Description"] = $"Zmiana przypisania i kolejności przystanków trasy #{id}.";
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

            TempData["Success"] = "Trasa została zaktualizowana.";
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
                ? "Trasa została usunięta. Możesz ponownie wygenerować plan dnia."
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
    public async Task<IActionResult> RouteMap(int? id, DateTimeOffset? date)
    {
        IReadOnlyList<DeliveryRouteDto> routes;
        DateTimeOffset selectedDate;

        if (id.HasValue)
        {
            var route = await _deliveryRouteService.GetRouteDetailsAsync(id.Value);
            if (route == null) return NotFound();

            routes = new[] { route };
            selectedDate = route.RouteDate;
        }
        else
        {
            selectedDate = date ?? DateTimeOffset.Now;
            routes = await _deliveryRouteService.GetRoutesForDateAsync(selectedDate);
        }

        ViewData["Title"] = "Mapa tras";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = id.HasValue
            ? $"Przebieg trasy #{id.Value} i kolejność przystanków."
            : $"Trasy zaplanowane na {selectedDate:dd.MM.yyyy}.";
        return View(new RouteMapViewModel
        {
            SelectedDate = selectedDate,
            SelectedRouteId = id,
            Routes = routes,
        });
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
    public async Task<IActionResult> Drivers()
    {
        ViewData["Title"] = "Kierowcy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Profile kierowców uprawnionych do realizacji dostaw.";
        return View(await _driverService.GetAllAsync());
    }

    [HttpGet("drivers/create")]
    public async Task<IActionResult> CreateDriver()
    {
        SetCreateDriverViewData();
        return View("DriversCreate", await BuildDriverCreateViewModelAsync());
    }

    [HttpPost("drivers/create")]
    public async Task<IActionResult> CreateDriver(DriverCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetCreateDriverViewData();
            return View("DriversCreate", await BuildDriverCreateViewModelAsync(model.Driver));
        }

        try
        {
            await _driverService.CreateAsync(model.Driver);
            TempData["Success"] = "Profil kierowcy został utworzony.";
            return RedirectToAction(nameof(Drivers));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            SetCreateDriverViewData();
            return View("DriversCreate", await BuildDriverCreateViewModelAsync(model.Driver));
        }
    }

    [HttpGet("drivers/edit/{id:int}")]
    public async Task<IActionResult> EditDriver(int id)
    {
        var driver = await _driverService.GetByIdAsync(id);
        if (driver == null) return NotFound();

        SetEditDriverViewData(driver.FullName);
        return View("DriversEdit", BuildDriverEditViewModel(driver));
    }

    [HttpPost("drivers/edit/{id:int}")]
    public async Task<IActionResult> EditDriver(int id, DriverEditViewModel model)
    {
        if (id != model.Driver.Id) return BadRequest();

        var existing = await _driverService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        if (!ModelState.IsValid)
        {
            SetEditDriverViewData(existing.FullName);
            return View("DriversEdit", BuildDriverEditViewModel(existing, model.Driver));
        }

        try
        {
            await _driverService.UpdateAsync(id, model.Driver);
            TempData["Success"] = "Profil kierowcy został zaktualizowany.";
            return RedirectToAction(nameof(Drivers));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            SetEditDriverViewData(existing.FullName);
            return View("DriversEdit", BuildDriverEditViewModel(existing, model.Driver));
        }
    }

    [HttpPost("drivers/delete/{id:int}")]
    public async Task<IActionResult> DeleteDriver(int id)
    {
        var deleted = await _driverService.DeleteAsync(id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Profil kierowcy został usunięty."
            : "Nie znaleziono profilu kierowcy.";
        return RedirectToAction(nameof(Drivers));
    }

    [HttpGet("drivers/{id:int}/vehicle")]
    public async Task<IActionResult> AssignDriverVehicle(int id)
    {
        var model = await BuildDriverVehicleAssignmentViewModelAsync(id);
        if (model == null) return NotFound();

        SetAssignDriverVehicleViewData(model.Driver.FullName);
        return View("DriversVehicleAssignment", model);
    }

    [HttpPost("drivers/{id:int}/vehicle")]
    public async Task<IActionResult> AssignDriverVehicle(int id, DriverVehicleAssignmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildDriverVehicleAssignmentViewModelAsync(id, model.VehicleId);
            if (invalidModel == null) return NotFound();

            SetAssignDriverVehicleViewData(invalidModel.Driver.FullName);
            return View("DriversVehicleAssignment", invalidModel);
        }

        try
        {
            var driver = await _driverService.AssignVehicleAsync(id, model.VehicleId);
            if (driver == null) return NotFound();

            TempData["Success"] = $"Kierowca {driver.FullName} zostal przypisany do pojazdu {driver.CurrentVehicleRegistration}.";
            return RedirectToAction(nameof(Drivers));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var invalidModel = await BuildDriverVehicleAssignmentViewModelAsync(id, model.VehicleId);
            if (invalidModel == null) return NotFound();

            SetAssignDriverVehicleViewData(invalidModel.Driver.FullName);
            return View("DriversVehicleAssignment", invalidModel);
        }
    }

    [HttpPost("drivers/{id:int}/vehicle/unassign")]
    public async Task<IActionResult> UnassignDriverVehicle(int id)
    {
        var driver = await _driverService.GetByIdAsync(id);
        if (driver == null) return NotFound();

        var unassigned = await _driverService.UnassignVehicleAsync(id);
        TempData[unassigned ? "Success" : "Error"] = unassigned
            ? $"Zakonczono przypisanie pojazdu dla kierowcy {driver.FullName}."
            : "Kierowca nie ma aktywnego przypisania do pojazdu.";
        return RedirectToAction(nameof(Drivers));
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
            DriverId = route.DriverId,
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
        var drivers = (await _driverService.GetAllAsync())
            .Where(driver => driver.IsActive)
            .OrderByDescending(driver => request.VehicleId > 0 && driver.CurrentVehicleId == request.VehicleId)
            .ThenBy(driver => driver.LastName)
            .ThenBy(driver => driver.FirstName)
            .ToList();

        return new RouteEditViewModel
        {
            Route = request,
            Vehicles = vehicles,
            Drivers = drivers,
            Stops = route.Stops
                .OrderBy(stop => stop.SequenceNumber)
                .ToList(),
        };
    }

    private async Task<LogisticsDashboardViewModel> BuildDashboardViewModelAsync(DateTimeOffset selectedDate)
    {
        var deliveriesTask = _deliveryDataProvider.GetDeliveriesForDateAsync(selectedDate.Date);
        var routesTask = _deliveryRouteService.GetRoutesForDateAsync(selectedDate);
        var vehiclesTask = _vehicleService.GetAllAsync();
        var driversTask = _driverService.GetAllAsync();
        var pendingAddressesTask = _addressRepository.GetPendingAddressesAsync();

        await Task.WhenAll(deliveriesTask, routesTask, vehiclesTask, driversTask, pendingAddressesTask);

        return new LogisticsDashboardViewModel
        {
            SelectedDate = selectedDate,
            Deliveries = await deliveriesTask,
            Routes = await routesTask,
            Vehicles = (await vehiclesTask).ToList(),
            Drivers = (await driversTask).ToList(),
            PendingAddressesCount = (await pendingAddressesTask).Count(),
        };
    }

    private async Task<DriverCreateViewModel> BuildDriverCreateViewModelAsync(CreateDriverRequest? request = null)
    {
        return new DriverCreateViewModel
        {
            Driver = request ?? new CreateDriverRequest(),
            AvailableUsers = await _driverService.GetAssignableUsersAsync(),
        };
    }

    private static DriverEditViewModel BuildDriverEditViewModel(
        DriverDto driver,
        UpdateDriverRequest? request = null)
    {
        return new DriverEditViewModel
        {
            Driver = request ?? new UpdateDriverRequest
            {
                Id = driver.Id,
                UserId = driver.UserId,
                LicenseNumber = driver.LicenseNumber,
                IsActive = driver.IsActive,
            },
            FullName = driver.FullName,
            Email = driver.Email,
        };
    }

    private void SetCreateDriverViewData()
    {
        ViewData["Title"] = "Nowy kierowca";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Utwórz profil logistyczny dla istniejącego użytkownika.";
    }

    private void SetEditDriverViewData(string fullName)
    {
        ViewData["Title"] = "Edycja kierowcy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Zmiana danych profilu kierowcy {fullName}.";
    }

    private async Task<DriverVehicleAssignmentViewModel?> BuildDriverVehicleAssignmentViewModelAsync(
        int driverId,
        int? selectedVehicleId = null)
    {
        var driver = await _driverService.GetByIdAsync(driverId);
        if (driver == null) return null;

        var drivers = (await _driverService.GetAllAsync()).ToList();
        var assignedDrivers = drivers
            .Where(item => item.CurrentVehicleId.HasValue)
            .ToDictionary(item => item.CurrentVehicleId!.Value);
        var vehicles = (await _vehicleService.GetAllAsync())
            .Where(vehicle => vehicle.Status == VehicleStatus.Active.ToString())
            .OrderBy(vehicle => vehicle.RegistrationNumber)
            .Select(vehicle =>
            {
                assignedDrivers.TryGetValue(vehicle.Id, out var assignedDriver);
                return new DriverVehicleOptionViewModel
                {
                    VehicleId = vehicle.Id,
                    RegistrationNumber = vehicle.RegistrationNumber,
                    Model = vehicle.Model,
                    IsAvailable = assignedDriver == null || assignedDriver.Id == driver.Id,
                    IsCurrentForDriver = driver.CurrentVehicleId == vehicle.Id,
                    AssignedDriverName = assignedDriver?.FullName,
                };
            })
            .ToList();

        return new DriverVehicleAssignmentViewModel
        {
            Driver = driver,
            VehicleId = selectedVehicleId ?? driver.CurrentVehicleId ?? 0,
            Vehicles = vehicles,
        };
    }

    private void SetAssignDriverVehicleViewData(string fullName)
    {
        ViewData["Title"] = "Przypisanie pojazdu";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Wybierz pojazd dla kierowcy {fullName}.";
    }
}
