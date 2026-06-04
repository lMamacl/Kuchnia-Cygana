using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Domain.Interfaces.Packing;

namespace KuchniaUCygana.Application.Services.Logistics;

public sealed class DriverMobileService : IDriverMobileService
{
    private readonly ICurrentUserService currentUserService;
    private readonly IUserRepository userRepository;
    private readonly IDriverRepository driverRepository;
    private readonly IDriverVehicleAssignmentRepository assignmentRepository;
    private readonly IVehicleRepository vehicleRepository;
    private readonly IDeliveryRouteRepository routeRepository;
    private readonly IDeliveryRouteStopRepository stopRepository;
    private readonly IDeliveryIssueRepository issueRepository;
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;
    private readonly ILogisticsDeliveryDataProvider deliveryDataProvider;
    private readonly IPackingSessionRepository packingSessionRepository;
    private readonly IPackingBagRepository packingBagRepository;
    private readonly IPackingManifestRepository manifestRepository;

    public DriverMobileService(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IDriverRepository driverRepository,
        IDriverVehicleAssignmentRepository assignmentRepository,
        IVehicleRepository vehicleRepository,
        IDeliveryRouteRepository routeRepository,
        IDeliveryRouteStopRepository stopRepository,
        IDeliveryIssueRepository issueRepository,
        IDeliveryCalendarRepository deliveryCalendarRepository,
        ILogisticsDeliveryDataProvider deliveryDataProvider,
        IPackingSessionRepository packingSessionRepository,
        IPackingBagRepository packingBagRepository,
        IPackingManifestRepository manifestRepository)
    {
        this.currentUserService = currentUserService;
        this.userRepository = userRepository;
        this.driverRepository = driverRepository;
        this.assignmentRepository = assignmentRepository;
        this.vehicleRepository = vehicleRepository;
        this.routeRepository = routeRepository;
        this.stopRepository = stopRepository;
        this.issueRepository = issueRepository;
        this.deliveryCalendarRepository = deliveryCalendarRepository;
        this.deliveryDataProvider = deliveryDataProvider;
        this.packingSessionRepository = packingSessionRepository;
        this.packingBagRepository = packingBagRepository;
        this.manifestRepository = manifestRepository;
    }

    public async Task<DriverDashboardDto> GetDashboardAsync()
    {
        var context = await this.LoadContextAsync();
        return new DriverDashboardDto
        {
            DeliveryDate = context.DeliveryDate,
            DriverName = context.DriverName,
            SetupMessage = context.SetupMessage,
            Vehicle = MapVehicle(context.Vehicle),
            Route = context.Route is null ? null : await this.MapRouteAsync(context),
        };
    }

    public async Task<DriverRouteStopDto?> GetStopAsync(int stopId)
    {
        var context = await this.LoadContextAsync();
        if (context.Route is null)
        {
            return null;
        }

        var route = await this.MapRouteAsync(context);
        return route.Stops.SingleOrDefault(stop => stop.Id == stopId);
    }

    public async Task StartRouteAsync()
    {
        var context = await this.RequireRouteContextAsync();
        var routeDto = await this.MapRouteAsync(context);

        if (context.Route!.Status == RouteStatus.InProgress)
        {
            return;
        }

        if (context.Route.Status != RouteStatus.Assigned)
        {
            throw new InvalidOperationException("Tej trasy nie mozna juz rozpoczac.");
        }

        if (!routeDto.IsReadyForDeparture)
        {
            throw new InvalidOperationException(routeDto.ReadinessMessage);
        }

        context.Route.Status = RouteStatus.InProgress;
        await this.routeRepository.UpdateAsync(context.Route);
    }

    public async Task StartStopAsync(int stopId)
    {
        var context = await this.RequireRouteContextAsync();
        var route = context.Route!;
        EnsureRouteInProgress(route);
        var stop = RequireStop(route, stopId);

        if (stop.Status == StopStatus.InProgress)
        {
            return;
        }

        EnsureNextStop(route, stop);
        stop.Status = StopStatus.InProgress;
        stop.ActualArrivalTime ??= DateTimeOffset.UtcNow;
        await this.stopRepository.UpdateAsync(stop);
    }

    public async Task ConfirmDeliveryAsync(int stopId, ConfirmDriverDeliveryRequest request)
    {
        var context = await this.RequireRouteContextAsync();
        var route = context.Route!;
        EnsureRouteInProgress(route);
        var stop = RequireActiveStop(route, stopId);
        var routeDto = await this.MapRouteAsync(context);
        var stopDto = routeDto.Stops.Single(dto => dto.Id == stopId);
        ValidateBagCodes(stopDto.Bags, request.BagCodes);

        foreach (var session in context.PackingSessions.Where(session => session.DeliveryCalendarId == stop.DeliveryCalendarId))
        {
            if (session.Status == PackingStatus.Dispatched)
            {
                session.Status = PackingStatus.Delivered;
                await this.packingSessionRepository.UpdateAsync(session);
            }
        }

        await this.UpdateDeliveryCalendarAsync(stop.DeliveryCalendarId, DeliveryStatus.Delivered);
        stop.Status = StopStatus.Completed;
        stop.ActualArrivalTime ??= DateTimeOffset.UtcNow;
        await this.stopRepository.UpdateAsync(stop);
        await this.RefreshRouteStatusAsync(route);
    }

    public async Task ReportProblemAsync(int stopId, ReportDriverDeliveryProblemRequest request)
    {
        var context = await this.RequireRouteContextAsync();
        var route = context.Route!;
        EnsureRouteInProgress(route);
        var stop = RequireActiveStop(route, stopId);
        var reason = RequireText(request.Reason, 100, "Wybierz powod problemu.");
        var notes = NormalizeOptionalText(request.Notes, 500);
        var now = DateTimeOffset.UtcNow;

        await this.issueRepository.InsertAsync(new DeliveryIssue
        {
            RouteStopId = stop.Id,
            DriverId = context.Driver!.Id,
            Reason = reason,
            Notes = notes,
            ReportedAt = now,
            CreatedBy = this.currentUserService.GetUserName(),
        });

        foreach (var session in context.PackingSessions.Where(session => session.DeliveryCalendarId == stop.DeliveryCalendarId))
        {
            if (session.Status == PackingStatus.Dispatched)
            {
                session.Status = PackingStatus.DeliveryFailed;
                await this.packingSessionRepository.UpdateAsync(session);
            }
        }

        await this.UpdateDeliveryCalendarAsync(stop.DeliveryCalendarId, DeliveryStatus.Skipped, reason);
        stop.Status = StopStatus.Failed;
        stop.ActualArrivalTime ??= now;
        await this.stopRepository.UpdateAsync(stop);
        await this.RefreshRouteStatusAsync(route);
    }

    public async Task<DriverRouteSummaryDto> GetSummaryAsync()
    {
        var context = await this.LoadContextAsync();
        return new DriverRouteSummaryDto
        {
            DeliveryDate = context.DeliveryDate,
            DriverName = context.DriverName,
            SetupMessage = context.SetupMessage,
            Vehicle = MapVehicle(context.Vehicle),
            Route = context.Route is null ? null : await this.MapRouteAsync(context),
        };
    }

    private async Task<DriverContext> LoadContextAsync()
    {
        var deliveryDate = DateOnly.FromDateTime(DateTime.Now);
        var userId = this.currentUserService.GetUserId();
        if (!userId.HasValue)
        {
            return DriverContext.WithMessage(deliveryDate, string.Empty, "Brak identyfikatora zalogowanego kierowcy.");
        }

        var user = await this.userRepository.GetByIdAsync(userId.Value);
        var driverName = FormatDriverName(user);
        var driver = await this.driverRepository.GetByUserIdAsync(userId.Value);
        if (driver is null || !driver.IsActive)
        {
            return DriverContext.WithMessage(deliveryDate, driverName, "Twoj aktywny profil kierowcy nie jest jeszcze skonfigurowany.");
        }

        var assignment = await this.assignmentRepository.GetActiveByDriverIdAsync(driver.Id);
        if (assignment is null)
        {
            return DriverContext.WithMessage(deliveryDate, driverName, "Dyspozytor nie przypisal jeszcze pojazdu do Twojego profilu.", driver);
        }

        var vehicle = await this.vehicleRepository.GetByIdAsync(assignment.VehicleId);
        if (vehicle is null)
        {
            return DriverContext.WithMessage(deliveryDate, driverName, "Nie znaleziono przypisanego pojazdu.", driver);
        }

        var routeDate = new DateTimeOffset(deliveryDate.ToDateTime(TimeOnly.MinValue));
        var route = (await this.routeRepository.GetRoutesWithStopsAsync(routeDate))
            .Where(candidate => candidate.VehicleId == vehicle.Id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefault();

        if (route is null)
        {
            return DriverContext.WithMessage(
                deliveryDate,
                driverName,
                "Na dzisiaj nie przypisano trasy do Twojego pojazdu.",
                driver,
                vehicle);
        }

        var packingSessions = (await this.packingSessionRepository.GetByDateWithItemsAsync(deliveryDate))
            .Where(session => session.DeliveryCalendarId.HasValue)
            .ToList();

        return new DriverContext(deliveryDate, driverName, driver, vehicle, route, packingSessions, null);
    }

    private async Task<DriverContext> RequireRouteContextAsync()
    {
        var context = await this.LoadContextAsync();
        if (context.Driver is null || context.Vehicle is null || context.Route is null)
        {
            throw new InvalidOperationException(context.SetupMessage ?? "Nie znaleziono dzisiejszej trasy.");
        }

        return context;
    }

    private async Task<DriverRouteDto> MapRouteAsync(DriverContext context)
    {
        var route = context.Route!;
        var stops = route.Stops.OrderBy(stop => stop.SequenceNumber).ToList();
        var deliveryCalendarIds = stops.Select(stop => stop.DeliveryCalendarId).ToArray();
        var deliveries = (await this.deliveryDataProvider.GetDeliveriesByCalendarIdsAsync(deliveryCalendarIds))
            .ToDictionary(delivery => delivery.DeliveryCalendarId);
        var sessions = context.PackingSessions
            .Where(session => session.DeliveryCalendarId.HasValue && deliveryCalendarIds.Contains(session.DeliveryCalendarId.Value))
            .ToList();
        var sessionIds = sessions.Select(session => session.Id).ToArray();
        var bags = await this.packingBagRepository.GetBySessionIdsAsync(sessionIds);
        var bagsBySession = bags.GroupBy(bag => bag.PackingSessionId).ToDictionary(group => group.Key, group => group.ToList());
        var sessionsByDelivery = sessions
            .GroupBy(session => session.DeliveryCalendarId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var issues = await this.issueRepository.GetByRouteStopIdsAsync(stops.Select(stop => stop.Id));
        var latestIssues = issues
            .GroupBy(issue => issue.RouteStopId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(issue => issue.ReportedAt).First());
        var nextStopId = stops
            .Where(stop => stop.Status is StopStatus.Created or StopStatus.Assigned or StopStatus.InProgress)
            .OrderBy(stop => stop.SequenceNumber)
            .Select(stop => (int?)stop.Id)
            .FirstOrDefault();
        var manifest = await this.manifestRepository.GetLatestAsync(context.DeliveryDate, route.Id);
        var allStopsHaveSessions = stops.Count > 0 && stops.All(stop => sessionsByDelivery.ContainsKey(stop.DeliveryCalendarId));
        var allSessionsDispatched = sessions.Count > 0 &&
            sessions.All(session => session.Status is PackingStatus.Dispatched or PackingStatus.Delivered or PackingStatus.DeliveryFailed);
        var isManifestReady = manifest is
        {
            IsVerified: true,
            RequiresRegeneration: false,
            SentToLogisticsAt: not null,
        };
        var isReady = allStopsHaveSessions && allSessionsDispatched && isManifestReady;

        return new DriverRouteDto
        {
            Id = route.Id,
            Name = route.Name,
            DeliveryDate = context.DeliveryDate,
            TotalDistanceKm = route.TotalDistanceKm,
            Status = route.Status.ToString(),
            IsReadyForDeparture = isReady,
            ReadinessMessage = GetReadinessMessage(isManifestReady, allStopsHaveSessions, allSessionsDispatched),
            CompletedStops = stops.Count(stop => stop.Status == StopStatus.Completed),
            FailedStops = stops.Count(stop => stop.Status == StopStatus.Failed),
            Stops = stops.Select(stop =>
            {
                deliveries.TryGetValue(stop.DeliveryCalendarId, out var delivery);
                sessionsByDelivery.TryGetValue(stop.DeliveryCalendarId, out var stopSessions);
                latestIssues.TryGetValue(stop.Id, out var issue);
                var isNext = stop.Id == nextStopId;

                return new DriverRouteStopDto
                {
                    Id = stop.Id,
                    DeliveryCalendarId = stop.DeliveryCalendarId,
                    SequenceNumber = stop.SequenceNumber,
                    FullAddress = delivery?.FullAddress ?? $"Dostawa #{stop.DeliveryCalendarId}",
                    Latitude = delivery?.Latitude,
                    Longitude = delivery?.Longitude,
                    PlannedArrivalTime = stop.PlannedArrivalTime,
                    ActualArrivalTime = stop.ActualArrivalTime,
                    Status = stop.Status.ToString(),
                    OrderNumber = delivery?.OrderNumber,
                    Bags = MapBags(stopSessions, bagsBySession),
                    LatestIssue = issue is null ? null : new DriverDeliveryIssueDto
                    {
                        Reason = issue.Reason,
                        Notes = issue.Notes,
                        ReportedAt = issue.ReportedAt,
                    },
                    IsNext = isNext,
                    CanStart = route.Status == RouteStatus.InProgress && isNext && stop.Status is StopStatus.Created or StopStatus.Assigned,
                    CanConfirm = route.Status == RouteStatus.InProgress && isNext && stop.Status == StopStatus.InProgress,
                    CanReportProblem = route.Status == RouteStatus.InProgress && isNext && stop.Status == StopStatus.InProgress,
                };
            }).ToList(),
        };
    }

    private static List<DriverDeliveryBagDto> MapBags(
        IReadOnlyCollection<PackingSession>? sessions,
        IReadOnlyDictionary<int, List<PackingBag>> bagsBySession)
    {
        if (sessions is null)
        {
            return new List<DriverDeliveryBagDto>();
        }

        return sessions
            .SelectMany(session => bagsBySession.GetValueOrDefault(session.Id) ?? Enumerable.Empty<PackingBag>())
            .OrderBy(bag => bag.BagNumber)
            .ThenBy(bag => bag.Id)
            .Select(bag => new DriverDeliveryBagDto
            {
                Id = bag.Id,
                BagNumber = bag.BagNumber,
                BagCode = bag.BagCode,
                Status = bag.Status.ToString(),
            })
            .ToList();
    }

    private async Task UpdateDeliveryCalendarAsync(int deliveryCalendarId, DeliveryStatus status, string? skipReason = null)
    {
        var delivery = await this.deliveryCalendarRepository.GetByIdAsync(deliveryCalendarId);
        if (delivery is null)
        {
            throw new InvalidOperationException("Nie znaleziono wpisu kalendarza dostawy.");
        }

        delivery.Status = status;
        delivery.IsSkipped = status == DeliveryStatus.Skipped;
        delivery.SkipReason = status == DeliveryStatus.Skipped ? skipReason : null;
        await this.deliveryCalendarRepository.UpdateAsync(delivery);
    }

    private async Task RefreshRouteStatusAsync(DeliveryRoute route)
    {
        if (route.Stops.All(stop => stop.Status is StopStatus.Completed or StopStatus.Failed))
        {
            route.Status = route.Stops.Any(stop => stop.Status == StopStatus.Failed)
                ? RouteStatus.Failed
                : RouteStatus.Completed;
            await this.routeRepository.UpdateAsync(route);
        }
    }

    private static DeliveryRouteStop RequireStop(DeliveryRoute route, int stopId)
    {
        return route.Stops.SingleOrDefault(stop => stop.Id == stopId)
            ?? throw new InvalidOperationException("Ten przystanek nie nalezy do Twojej dzisiejszej trasy.");
    }

    private static DeliveryRouteStop RequireActiveStop(DeliveryRoute route, int stopId)
    {
        var stop = RequireStop(route, stopId);
        if (stop.Status != StopStatus.InProgress)
        {
            throw new InvalidOperationException("Najpierw rozpocznij obsluge tego przystanku.");
        }

        EnsureNextStop(route, stop);
        return stop;
    }

    private static void EnsureNextStop(DeliveryRoute route, DeliveryRouteStop requestedStop)
    {
        var nextStop = route.Stops
            .Where(stop => stop.Status is StopStatus.Created or StopStatus.Assigned or StopStatus.InProgress)
            .OrderBy(stop => stop.SequenceNumber)
            .FirstOrDefault();

        if (nextStop?.Id != requestedStop.Id)
        {
            throw new InvalidOperationException("Najpierw zakoncz poprzedni przystanek na trasie.");
        }
    }

    private static void EnsureRouteInProgress(DeliveryRoute route)
    {
        if (route.Status != RouteStatus.InProgress)
        {
            throw new InvalidOperationException("Najpierw rozpocznij trase.");
        }
    }

    private static void ValidateBagCodes(
        IReadOnlyCollection<DriverDeliveryBagDto> bags,
        IReadOnlyCollection<string> submittedCodes)
    {
        var expected = bags.Select(bag => bag.BagCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var submitted = submittedCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!expected.SetEquals(submitted))
        {
            throw new InvalidOperationException("Zeskanuj wszystkie i tylko te torby, ktore naleza do tego przystanku.");
        }
    }

    private static string GetReadinessMessage(bool isManifestReady, bool allStopsHaveSessions, bool allSessionsDispatched)
    {
        if (!isManifestReady)
        {
            return "Magazyn nie przekazal jeszcze finalnego manifestu do logistyki.";
        }

        if (!allStopsHaveSessions)
        {
            return "Nie wszystkie przystanki maja kompletacje powiazana z DeliveryCalendarId.";
        }

        if (!allSessionsDispatched)
        {
            return "Magazyn nie zakonczyl jeszcze wydawania wszystkich toreb.";
        }

        return "Auto zostalo wydane przez magazyn. Mozesz rozpoczac trase.";
    }

    private static DriverVehicleDto? MapVehicle(Vehicle? vehicle)
    {
        return vehicle is null
            ? null
            : new DriverVehicleDto
            {
                Id = vehicle.Id,
                RegistrationNumber = vehicle.RegistrationNumber,
                Model = vehicle.Model,
            };
    }

    private static string FormatDriverName(User? user)
    {
        if (user is null)
        {
            return "Kierowca";
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static string RequireText(string? value, int maxLength, string errorMessage)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record DriverContext(
        DateOnly DeliveryDate,
        string DriverName,
        Driver? Driver,
        Vehicle? Vehicle,
        DeliveryRoute? Route,
        IReadOnlyList<PackingSession> PackingSessions,
        string? SetupMessage)
    {
        public static DriverContext WithMessage(
            DateOnly date,
            string driverName,
            string message,
            Driver? driver = null,
            Vehicle? vehicle = null)
        {
            return new DriverContext(date, driverName, driver, vehicle, null, Array.Empty<PackingSession>(), message);
        }
    }
}
