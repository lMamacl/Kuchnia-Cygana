using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;
using KuchniaUCygana.Web.Models.Logistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Logistics,LogisticsManager,Admin")]
[Route("logistics")]
public sealed class LogisticsController : Controller
{
    private const int LookupPageSize = 100;

    private readonly IVehicleService _vehicleService;
    private readonly IDriverService _driverService;
    private readonly IGeocodeService _geocodeService;
    private readonly GeocodingOrchestrator _geocodingOrchestrator;
    private readonly ILogisticsDeliveryDataProvider _deliveryDataProvider;
    private readonly IAddressRepository _addressRepository;
    private readonly IDeliveryRouteService _deliveryRouteService;
    private readonly IPackingSynchronizationService _packingSynchronizationService;
    private readonly ILogisticsSbdReportRepository _logisticsSbdReportRepository;

    public LogisticsController(
        IVehicleService vehicleService,
        IDriverService driverService,
        IGeocodeService geocodeService,
        GeocodingOrchestrator geocodingOrchestrator,
        ILogisticsDeliveryDataProvider deliveryDataProvider,
        IAddressRepository addressRepository,
        IDeliveryRouteService deliveryRouteService,
        IPackingSynchronizationService packingSynchronizationService,
        ILogisticsSbdReportRepository logisticsSbdReportRepository)
    {
        _vehicleService = vehicleService;
        _driverService = driverService;
        _geocodeService = geocodeService;
        _geocodingOrchestrator = geocodingOrchestrator;
        _deliveryDataProvider = deliveryDataProvider;
        _addressRepository = addressRepository;
        _deliveryRouteService = deliveryRouteService;
        _packingSynchronizationService = packingSynchronizationService;
        _logisticsSbdReportRepository = logisticsSbdReportRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateTimeOffset? date, decimal? weight)
    {
        var selectedDate = date ?? DateTimeOffset.Now;
        var estWeight = weight ?? 1.20m;
        ViewData["Title"] = "Logistyka";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = $"Gotowość dostaw na {selectedDate:dd.MM.yyyy}.";
        return View(await BuildDashboardViewModelAsync(selectedDate, estWeight));
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
        if (result.Succeeded)
        {
            var sync = await _packingSynchronizationService.RefreshFromRoutesAsync(
                DateOnly.FromDateTime(request.RouteDate.Date),
                User.Identity?.Name ?? "Logistics");
            TempData["Success"] =
                $"Wygenerowano trasy i odświeżono kompletację: sesje +{sync.CreatedSessions}, torby +{sync.CreatedBags}, pudełka +{sync.CreatedItems}.";
        }

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
    public async Task<IActionResult> Vehicles([FromQuery] VehicleListFilterViewModel filter)
    {
        ViewData["Title"] = "Flota";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Zarządzanie flotą pojazdów dostawczych i ich gotowością operacyjną.";
        var page = await _vehicleService.SearchAsync(new VehicleSearchRequest
        {
            Search = filter.Search,
            Status = filter.Status,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        return View(new VehicleListViewModel
        {
            Filter = filter,
            Page = page,
        });
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
    public async Task<IActionResult> Drivers([FromQuery] DriverListFilterViewModel filter)
    {
        ViewData["Title"] = "Kierowcy";
        ViewData["Section"] = "Logistyka";
        ViewData["Description"] = "Profile kierowców uprawnionych do realizacji dostaw.";
        var page = await _driverService.SearchAsync(new DriverSearchRequest
        {
            Search = filter.Search,
            IsActive = filter.IsActive,
            HasVehicleAssignment = filter.HasVehicleAssignment,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        return View(new DriverListViewModel
        {
            Filter = filter,
            Page = page,
        });
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

        var vehiclesTask = GetActiveVehicleLookupAsync();
        var driversTask = GetDriverLookupAsync(isActive: true, hasVehicleAssignment: null);
        await Task.WhenAll(vehiclesTask, driversTask);

        var vehicles = await vehiclesTask;
        var drivers = (await driversTask)
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

    private async Task<LogisticsDashboardViewModel> BuildDashboardViewModelAsync(DateTimeOffset selectedDate, decimal estimatedDeliveryWeightKg)
    {
        var deliveriesTask = _deliveryDataProvider.GetDeliveriesForDateAsync(selectedDate.Date);
        var routesTask = _deliveryRouteService.GetRoutesForDateAsync(selectedDate);
        var vehicleSummaryTask = _vehicleService.SearchAsync(new VehicleSearchRequest
        {
            Page = 1,
            PageSize = 5,
        });
        var driverSummaryTask = _driverService.SearchAsync(new DriverSearchRequest
        {
            Page = 1,
            PageSize = 5,
        });
        var pendingAddressesTask = _addressRepository.GetPendingAddressesAsync();
        var sbdDispatchReportTask = _logisticsSbdReportRepository.GetDailyDispatchBoardAsync(
            selectedDate.Date,
            estimatedDeliveryWeightKg: estimatedDeliveryWeightKg);

        await Task.WhenAll(
            deliveriesTask,
            routesTask,
            vehicleSummaryTask,
            driverSummaryTask,
            pendingAddressesTask,
            sbdDispatchReportTask.ContinueWith(_ => { }));

        var vehicleSummary = await vehicleSummaryTask;
        var driverSummary = await driverSummaryTask;

        LogisticsDailyDispatchBoardReport sbdDispatchReport;
        try
        {
            sbdDispatchReport = await sbdDispatchReportTask;
        }
        catch (System.Exception ex)
        {
            sbdDispatchReport = new LogisticsDailyDispatchBoardReport();
            ViewData["SbdReportError"] = ex.Message;
        }

        return new LogisticsDashboardViewModel
        {
            SelectedDate = selectedDate,
            Deliveries = await deliveriesTask,
            Routes = await routesTask,
            ActiveVehiclesCount = vehicleSummary.ActiveCount,
            ActiveVehiclesCapacityKg = vehicleSummary.ActiveCapacityKg,
            ActiveDriversCount = driverSummary.ActiveCount,
            DriversWithVehicleCount = driverSummary.WithVehicleCount,
            PendingAddressesCount = (await pendingAddressesTask).Count(),
            SbdDispatchReport = sbdDispatchReport,
        };
    }

    private async Task<IReadOnlyList<VehicleDto>> GetActiveVehicleLookupAsync()
    {
        var vehicles = new List<VehicleDto>();
        var page = 1;

        while (true)
        {
            var result = await _vehicleService.SearchAsync(new VehicleSearchRequest
            {
                Status = VehicleStatus.Active.ToString(),
                Page = page,
                PageSize = LookupPageSize,
            });

            vehicles.AddRange(result.Items);
            if (vehicles.Count >= result.TotalCount || result.Items.Count == 0)
            {
                break;
            }

            page++;
        }

        return vehicles
            .OrderBy(vehicle => vehicle.RegistrationNumber)
            .ThenBy(vehicle => vehicle.Id)
            .ToList();
    }

    private async Task<IReadOnlyList<DriverDto>> GetDriverLookupAsync(bool? isActive, bool? hasVehicleAssignment)
    {
        var drivers = new List<DriverDto>();
        var page = 1;

        while (true)
        {
            var result = await _driverService.SearchAsync(new DriverSearchRequest
            {
                IsActive = isActive,
                HasVehicleAssignment = hasVehicleAssignment,
                Page = page,
                PageSize = LookupPageSize,
            });

            drivers.AddRange(result.Items);
            if (drivers.Count >= result.TotalCount || result.Items.Count == 0)
            {
                break;
            }

            page++;
        }

        return drivers
            .OrderBy(driver => driver.LastName)
            .ThenBy(driver => driver.FirstName)
            .ThenBy(driver => driver.Email)
            .ToList();
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

        var drivers = await GetDriverLookupAsync(isActive: null, hasVehicleAssignment: true);
        var assignedDrivers = drivers
            .Where(item => item.CurrentVehicleId.HasValue)
            .GroupBy(item => item.CurrentVehicleId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var vehicles = (await GetActiveVehicleLookupAsync())
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
