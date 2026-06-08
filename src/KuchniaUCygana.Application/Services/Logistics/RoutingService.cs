using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

/// <summary>
/// Koordynuje generowanie tras dziennych dla dyspozytora M4.
/// </summary>
public sealed class RoutingService : IDeliveryRouteService
{
    private const double AdditionalRouteActivationCostKm = 8d;
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IDeliveryRouteStopRepository _routeStopRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IDriverVehicleAssignmentRepository _driverVehicleAssignmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogisticsDeliveryDataProvider _deliveryDataProvider;
    private readonly IRouteOptimizer _routeOptimizer;

    public RoutingService(
        IDeliveryRouteRepository routeRepository,
        IDeliveryRouteStopRepository routeStopRepository,
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        IDriverVehicleAssignmentRepository driverVehicleAssignmentRepository,
        IUserRepository userRepository,
        ILogisticsDeliveryDataProvider deliveryDataProvider,
        IRouteOptimizer routeOptimizer)
    {
        _routeRepository = routeRepository;
        _routeStopRepository = routeStopRepository;
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _driverVehicleAssignmentRepository = driverVehicleAssignmentRepository;
        _userRepository = userRepository;
        _deliveryDataProvider = deliveryDataProvider;
        _routeOptimizer = routeOptimizer;
    }

    public async Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesForDateAsync(DateTimeOffset date)
    {
        var routes = await _routeRepository.GetRoutesWithStopsAsync(date);
        return await MapRoutesAsync(routes);
    }

    public async Task<DeliveryRouteDto?> GetRouteDetailsAsync(int routeId)
    {
        var route = await _routeRepository.GetRouteWithStopsAsync(routeId);
        if (route is null)
        {
            return null;
        }

        return (await MapRoutesAsync(new[] { route })).Single();
    }

    public async Task<DailyRouteGenerationResultDto> GenerateDailyRoutesAsync(GenerateDailyRoutesRequest request)
    {
        var routeDate = request.RouteDate.Date;
        var existingRoutes = await _routeRepository.GetRoutesWithStopsAsync(routeDate);
        if (existingRoutes.Count > 0)
        {
            return new DailyRouteGenerationResultDto
            {
                Succeeded = false,
                GeneratedRoutesCount = 0,
                PlannedStopsCount = existingRoutes.Sum(r => r.Stops.Count),
                Routes = (await MapRoutesAsync(existingRoutes)).ToList(),
                Issues =
                {
                    new RouteGenerationIssueDto
                    {
                        Code = "RoutesAlreadyExist",
                        Message = "Trasy dla wybranego dnia juz istnieja. Edytuj istniejace trasy albo usun je przed ponownym generowaniem.",
                    },
                },
            };
        }

        var deliveries = await _deliveryDataProvider.GetDeliveriesForDateAsync(
            routeDate,
            request.DefaultDeliveryLoadKg);

        if (deliveries.Count == 0)
        {
            return Failed("NoDeliveries", "Brak zaplanowanych dostaw dla wybranego dnia.");
        }

        var missingGeocoding = deliveries.Where(d => !d.HasCoordinates).ToList();
        var routableDeliveries = deliveries.Where(d => d.HasCoordinates).ToList();

        if (routableDeliveries.Count == 0)
        {
            return new DailyRouteGenerationResultDto
            {
                Succeeded = false,
                MissingGeocoding = missingGeocoding.Select(MapCandidate).ToList(),
                Issues =
                {
                    new RouteGenerationIssueDto
                    {
                        Code = "MissingGeocoding",
                        Message = "Zadna dostawa nie ma kompletnych wspolrzednych. Uruchom geokodowanie adresow przed generowaniem tras.",
                    },
                },
            };
        }

        var vehicles = (await _vehicleRepository.GetAllAsync())
            .Where(v => v.Status == VehicleStatus.Active)
            .OrderByDescending(v => v.MaxLoadKg)
            .ThenBy(v => v.RegistrationNumber)
            .ToList();

        if (vehicles.Count == 0)
        {
            return Failed("NoVehicles", "Brak aktywnych pojazdow dostepnych do wygenerowania tras.");
        }

        var batches = CreateBatches(routableDeliveries, vehicles, request.MaxStopsPerRoute);
        var createdRoutes = new List<DeliveryRoute>();

        foreach (var batch in batches.Where(b => b.Deliveries.Count > 0))
        {
            var route = await BuildRouteFromBatchAsync(routeDate, batch, createdRoutes.Count + 1);
            createdRoutes.Add(route);
        }

        await _routeRepository.InsertManyWithStopsAsync(createdRoutes);

        var unassigned = batches.SelectMany(b => b.UnassignedOverflow).ToList();
        var mappedRoutes = (await MapRoutesAsync(createdRoutes)).ToList();
        var issues = new List<RouteGenerationIssueDto>();

        if (missingGeocoding.Count > 0)
        {
            issues.Add(new RouteGenerationIssueDto
            {
                Code = "SomeMissingGeocoding",
                Message = "Czesc dostaw pominieto, bo ich adresy nie maja kompletnych wspolrzednych.",
            });
        }

        if (unassigned.Count > 0)
        {
            issues.Add(new RouteGenerationIssueDto
            {
                Code = "CapacityExceeded",
                Message = "Nie wszystkie dostawy zmiescily sie w dostepnych pojazdach.",
            });
        }

        return new DailyRouteGenerationResultDto
        {
            Succeeded = mappedRoutes.Count > 0 && unassigned.Count == 0,
            GeneratedRoutesCount = mappedRoutes.Count,
            PlannedStopsCount = mappedRoutes.Sum(r => r.Stops.Count),
            Routes = mappedRoutes,
            MissingGeocoding = missingGeocoding.Select(MapCandidate).ToList(),
            UnassignedDeliveries = unassigned.Select(MapCandidate).ToList(),
            Issues = issues,
        };
    }

    public async Task<DeliveryRouteDto?> UpdateRouteAsync(UpdateDeliveryRouteRequest request)
    {
        var route = await _routeRepository.GetRouteWithStopsAsync(request.Id);
        if (route is null)
        {
            return null;
        }

        EnsureRouteCanBeModified(route);

        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
        if (vehicle is null || vehicle.Status != VehicleStatus.Active)
        {
            throw new InvalidOperationException("Wybrany pojazd nie istnieje albo nie jest aktywny.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Nazwa trasy jest wymagana.");
        }

        var driverId = await ValidateOptionalDriverAsync(request.DriverId);
        ValidateStopSequence(route.Stops, request.Stops);

        var requestedStops = request.Stops.ToDictionary(stop => stop.StopId);
        foreach (var stop in route.Stops)
        {
            stop.SequenceNumber = requestedStops[stop.Id].SequenceNumber;
            await _routeStopRepository.UpdateAsync(stop);
        }

        route.Name = name;
        route.VehicleId = vehicle.Id;
        route.DriverId = driverId;
        route.Status = RouteStatus.Assigned;
        route.TotalDistanceKm = await CalculateRouteDistanceAsync(route.Stops);
        await _routeRepository.UpdateAsync(route);

        return await GetRouteDetailsAsync(route.Id);
    }

    public async Task<bool> DeleteRouteAsync(int routeId)
    {
        var route = await _routeRepository.GetRouteWithStopsAsync(routeId);
        if (route is null)
        {
            return false;
        }

        EnsureRouteCanBeModified(route);

        foreach (var stop in route.Stops)
        {
            await _routeStopRepository.DeleteAsync(stop.Id);
        }

        return await _routeRepository.DeleteAsync(route.Id);
    }

    private async Task<DeliveryRoute> BuildRouteFromBatchAsync(
        DateTimeOffset routeDate,
        RouteBatch batch,
        int routeNumber)
    {
        var optimizedIds = await _routeOptimizer.OptimizeSequenceAsync(
            batch.Deliveries.Select(ToOptimizationPoint).ToList());

        var deliveriesById = batch.Deliveries.ToDictionary(d => d.DeliveryCalendarId);
        var orderedDeliveries = optimizedIds.Select(id => deliveriesById[id]).ToList();
        var pointsById = batch.Deliveries.ToDictionary(d => d.DeliveryCalendarId, ToOptimizationPoint);
        var orderedPoints = optimizedIds.Select(id => pointsById[id]).ToList();

        var route = new DeliveryRoute
        {
            RouteDate = routeDate,
            Name = $"Trasa {routeNumber}",
            Status = RouteStatus.Assigned,
            VehicleId = batch.Vehicle.Id,
            TotalDistanceKm = Math.Round(NearestNeighborRouteOptimizer.CalculateRouteDistanceKm(orderedPoints), 2),
        };

        var sequence = 1;
        foreach (var delivery in orderedDeliveries)
        {
            var stop = new DeliveryRouteStop
            {
                DeliveryCalendarId = delivery.DeliveryCalendarId,
                SequenceNumber = sequence++,
                Status = StopStatus.Assigned,
            };
            route.Stops.Add(stop);
        }

        return route;
    }

    private static List<RouteBatch> CreateBatches(
        IReadOnlyList<LogisticsDeliveryCandidate> deliveries,
        IReadOnlyList<Vehicle> vehicles,
        int? maxStopsPerRoute)
    {
        var remaining = deliveries
            .OrderByDescending(d => d.EstimatedLoadKg)
            .ThenBy(d => d.DeliveryCalendarId)
            .ToList();
        var batches = new List<RouteBatch>();

        foreach (var vehicle in vehicles)
        {
            var batch = new RouteBatch(vehicle);

            while (remaining.Count > 0)
            {
                var next = SelectNextDelivery(batch, remaining, vehicle.MaxLoadKg, maxStopsPerRoute);
                if (next is null)
                {
                    break;
                }

                batch.Deliveries.Add(next);
                remaining.Remove(next);
            }

            batches.Add(batch);
        }

        if (remaining.Count > 0 && batches.Count > 0)
        {
            batches[^1].UnassignedOverflow.AddRange(remaining);
        }

        ImproveGeographicDistribution(batches, maxStopsPerRoute);

        return batches;
    }

    private static void ImproveGeographicDistribution(List<RouteBatch> batches, int? maxStopsPerRoute)
    {
        foreach (var targetBatch in batches.Where(batch => batch.Deliveries.Count == 0))
        {
            var bestSplit = batches
                .Where(sourceBatch => sourceBatch != targetBatch && sourceBatch.Deliveries.Count > 1)
                .Select(sourceBatch => TryCreateGeographicSplit(sourceBatch, targetBatch, maxStopsPerRoute))
                .Where(split => split is not null)
                .OrderByDescending(split => split!.DistanceSavingKm)
                .FirstOrDefault();

            if (bestSplit is null || bestSplit.DistanceSavingKm < AdditionalRouteActivationCostKm)
            {
                continue;
            }

            bestSplit.SourceBatch.Deliveries.Clear();
            bestSplit.SourceBatch.Deliveries.AddRange(bestSplit.SourceDeliveries);
            targetBatch.Deliveries.AddRange(bestSplit.TargetDeliveries);
        }
    }

    private static GeographicSplit? TryCreateGeographicSplit(
        RouteBatch sourceBatch,
        RouteBatch targetBatch,
        int? maxStopsPerRoute)
    {
        var farthestPair = sourceBatch.Deliveries
            .SelectMany(
                (source, sourceIndex) => sourceBatch.Deliveries
                    .Skip(sourceIndex + 1)
                    .Select(target => new
                    {
                        Source = source,
                        Target = target,
                        DistanceKm = DistanceBetween(source, target),
                    }))
            .OrderByDescending(pair => pair.DistanceKm)
            .FirstOrDefault();

        if (farthestPair is null)
        {
            return null;
        }

        var previousDistanceKm = EstimateRouteDistanceKm(sourceBatch.Deliveries);
        return new[]
            {
                BuildGeographicSplit(
                    sourceBatch,
                    targetBatch,
                    farthestPair.Source,
                    farthestPair.Target,
                    maxStopsPerRoute),
                BuildGeographicSplit(
                    sourceBatch,
                    targetBatch,
                    farthestPair.Target,
                    farthestPair.Source,
                    maxStopsPerRoute),
            }
            .Where(split => split is not null)
            .Select(split => split! with
            {
                DistanceSavingKm = previousDistanceKm
                    - EstimateRouteDistanceKm(split!.SourceDeliveries)
                    - EstimateRouteDistanceKm(split.TargetDeliveries),
            })
            .OrderByDescending(split => split.DistanceSavingKm)
            .FirstOrDefault();
    }

    private static GeographicSplit? BuildGeographicSplit(
        RouteBatch sourceBatch,
        RouteBatch targetBatch,
        LogisticsDeliveryCandidate sourceSeed,
        LogisticsDeliveryCandidate targetSeed,
        int? maxStopsPerRoute)
    {
        var sourceDeliveries = new List<LogisticsDeliveryCandidate>();
        var targetDeliveries = new List<LogisticsDeliveryCandidate>();

        if (!TryAddDelivery(sourceDeliveries, sourceSeed, sourceBatch.Vehicle, maxStopsPerRoute) ||
            !TryAddDelivery(targetDeliveries, targetSeed, targetBatch.Vehicle, maxStopsPerRoute))
        {
            return null;
        }

        var remaining = sourceBatch.Deliveries
            .Where(delivery =>
                delivery.DeliveryCalendarId != sourceSeed.DeliveryCalendarId &&
                delivery.DeliveryCalendarId != targetSeed.DeliveryCalendarId)
            .OrderByDescending(delivery =>
                Math.Abs(DistanceBetween(delivery, sourceSeed) - DistanceBetween(delivery, targetSeed)))
            .ThenBy(delivery => delivery.DeliveryCalendarId);

        foreach (var delivery in remaining)
        {
            var sourceDistanceKm = DistanceToClosest(delivery, sourceDeliveries);
            var targetDistanceKm = DistanceToClosest(delivery, targetDeliveries);
            var preferredDeliveries = sourceDistanceKm <= targetDistanceKm ? sourceDeliveries : targetDeliveries;
            var preferredVehicle = sourceDistanceKm <= targetDistanceKm ? sourceBatch.Vehicle : targetBatch.Vehicle;
            var fallbackDeliveries = sourceDistanceKm <= targetDistanceKm ? targetDeliveries : sourceDeliveries;
            var fallbackVehicle = sourceDistanceKm <= targetDistanceKm ? targetBatch.Vehicle : sourceBatch.Vehicle;

            if (!TryAddDelivery(preferredDeliveries, delivery, preferredVehicle, maxStopsPerRoute) &&
                !TryAddDelivery(fallbackDeliveries, delivery, fallbackVehicle, maxStopsPerRoute))
            {
                return null;
            }
        }

        return new GeographicSplit(sourceBatch, sourceDeliveries, targetDeliveries, 0d);
    }

    private static bool TryAddDelivery(
        ICollection<LogisticsDeliveryCandidate> deliveries,
        LogisticsDeliveryCandidate delivery,
        Vehicle vehicle,
        int? maxStopsPerRoute)
    {
        if (maxStopsPerRoute.HasValue && deliveries.Count >= maxStopsPerRoute.Value)
        {
            return false;
        }

        if (deliveries.Sum(item => item.EstimatedLoadKg) + delivery.EstimatedLoadKg > vehicle.MaxLoadKg)
        {
            return false;
        }

        deliveries.Add(delivery);
        return true;
    }

    private static double DistanceToClosest(
        LogisticsDeliveryCandidate delivery,
        IEnumerable<LogisticsDeliveryCandidate> cluster)
    {
        return cluster.Min(clusterDelivery => DistanceBetween(delivery, clusterDelivery));
    }

    private static double EstimateRouteDistanceKm(IReadOnlyCollection<LogisticsDeliveryCandidate> deliveries)
    {
        if (deliveries.Count <= 1)
        {
            return 0d;
        }

        var remaining = deliveries.Select(ToOptimizationPoint).ToList();
        var ordered = new List<RouteOptimizationPoint>(remaining.Count);
        var current = new RouteOptimizationPoint(
            0,
            remaining.Average(point => point.Latitude),
            remaining.Average(point => point.Longitude));

        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderBy(point => DistanceBetween(current, point))
                .ThenBy(point => point.Id)
                .First();

            ordered.Add(next);
            remaining.Remove(next);
            current = next;
        }

        return NearestNeighborRouteOptimizer.CalculateRouteDistanceKm(ordered);
    }

    private static LogisticsDeliveryCandidate? SelectNextDelivery(
        RouteBatch batch,
        IReadOnlyList<LogisticsDeliveryCandidate> remaining,
        decimal vehicleCapacityKg,
        int? maxStopsPerRoute)
    {
        if (maxStopsPerRoute.HasValue && batch.Deliveries.Count >= maxStopsPerRoute.Value)
        {
            return null;
        }

        var available = remaining
            .Where(d => batch.TotalLoadKg + d.EstimatedLoadKg <= vehicleCapacityKg)
            .ToList();

        if (available.Count == 0)
        {
            return null;
        }

        if (batch.Deliveries.Count == 0)
        {
            return available
                .OrderByDescending(d => d.EstimatedLoadKg)
                .ThenBy(d => d.DeliveryCalendarId)
                .First();
        }

        var last = batch.Deliveries[^1];
        return available
            .OrderBy(d => DistanceBetween(last, d))
            .ThenByDescending(d => d.EstimatedLoadKg)
            .ThenBy(d => d.DeliveryCalendarId)
            .First();
    }

    private async Task<IReadOnlyList<DeliveryRouteDto>> MapRoutesAsync(IEnumerable<DeliveryRoute> routes)
    {
        var routeList = routes.ToList();
        var deliveryCalendarIds = routeList
            .SelectMany(r => r.Stops)
            .Select(s => s.DeliveryCalendarId)
            .Distinct()
            .ToList();

        var deliveries = deliveryCalendarIds.Count == 0
            ? new Dictionary<int, LogisticsDeliveryCandidate>()
            : (await _deliveryDataProvider.GetDeliveriesByCalendarIdsAsync(deliveryCalendarIds))
                .ToDictionary(d => d.DeliveryCalendarId);

        var vehicleIds = routeList
            .Where(route => route.VehicleId.HasValue)
            .Select(route => route.VehicleId!.Value)
            .Distinct()
            .ToArray();
        var vehicles = vehicleIds.Length == 0
            ? new Dictionary<int, Vehicle>()
            : (await _vehicleRepository.GetByIdsAsync(vehicleIds)).ToDictionary(vehicle => vehicle.Id);
        var assignmentsByVehicle = vehicleIds.Length == 0
            ? new Dictionary<int, DriverVehicleAssignment>()
            : (await _driverVehicleAssignmentRepository.GetActiveByVehicleIdsAsync(vehicleIds))
                .GroupBy(assignment => assignment.VehicleId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.AssignedAt).First());
        var effectiveDriverIds = routeList
            .Select(route => GetEffectiveDriverId(route, assignmentsByVehicle))
            .Where(driverId => driverId.HasValue)
            .Select(driverId => driverId!.Value)
            .Distinct()
            .ToHashSet();
        var drivers = effectiveDriverIds.Count == 0
            ? new Dictionary<int, DriverDisplay>()
            : await BuildDriverDisplayLookupAsync(effectiveDriverIds);

        return routeList
            .OrderBy(r => r.Name)
            .Select(route =>
            {
                var effectiveDriverId = GetEffectiveDriverId(route, assignmentsByVehicle);

                return new DeliveryRouteDto
                {
                    Id = route.Id,
                    Name = route.Name,
                    RouteDate = route.RouteDate,
                    TotalDistanceKm = route.TotalDistanceKm,
                    Status = route.Status.ToString(),
                    DriverId = effectiveDriverId,
                    DriverName = effectiveDriverId.HasValue && drivers.TryGetValue(effectiveDriverId.Value, out var driver)
                        ? driver.FullName
                        : null,
                    VehicleId = route.VehicleId,
                    VehicleRegistration = route.VehicleId.HasValue && vehicles.TryGetValue(route.VehicleId.Value, out var vehicle)
                        ? vehicle.RegistrationNumber
                        : null,
                    Stops = route.Stops
                        .OrderBy(s => s.SequenceNumber)
                        .Select(stop => MapStop(stop, deliveries))
                        .ToList(),
                };
            })
            .ToList();
    }

    private async Task<int?> ValidateOptionalDriverAsync(int? driverId)
    {
        if (!driverId.HasValue || driverId.Value <= 0)
        {
            return null;
        }

        var driver = await _driverRepository.GetByIdAsync(driverId.Value);
        if (driver is null || !driver.IsActive)
        {
            throw new InvalidOperationException("Wybrany kierowca nie istnieje albo nie jest aktywny.");
        }

        return driver.Id;
    }

    private async Task<Dictionary<int, DriverDisplay>> BuildDriverDisplayLookupAsync(IReadOnlySet<int> driverIds)
    {
        var drivers = (await _driverRepository.GetByIdsAsync(driverIds.ToArray()))
            .ToDictionary(driver => driver.Id);
        var users = (await _userRepository.GetByIdsAsync(drivers.Values.Select(driver => driver.UserId).ToArray()))
            .ToDictionary(user => user.Id);

        return drivers
            .Select(pair =>
            {
                users.TryGetValue(pair.Value.UserId, out var user);
                var fullName = string.Join(
                    " ",
                    new[] { user?.FirstName, user?.LastName }
                        .Where(part => !string.IsNullOrWhiteSpace(part)));
                return new KeyValuePair<int, DriverDisplay>(
                    pair.Key,
                    new DriverDisplay(string.IsNullOrWhiteSpace(fullName) ? $"Kierowca #{pair.Key}" : fullName));
            })
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static int? GetEffectiveDriverId(
        DeliveryRoute route,
        IReadOnlyDictionary<int, DriverVehicleAssignment> assignmentsByVehicle)
    {
        if (route.VehicleId.HasValue &&
            assignmentsByVehicle.TryGetValue(route.VehicleId.Value, out var assignment))
        {
            return assignment.DriverId;
        }

        return route.DriverId;
    }

    private async Task<double> CalculateRouteDistanceAsync(IEnumerable<DeliveryRouteStop> stops)
    {
        var orderedStops = stops.OrderBy(stop => stop.SequenceNumber).ToList();
        var deliveryCalendarIds = orderedStops
            .Select(stop => stop.DeliveryCalendarId)
            .ToList();
        var deliveries = (await _deliveryDataProvider.GetDeliveriesByCalendarIdsAsync(deliveryCalendarIds))
            .ToDictionary(delivery => delivery.DeliveryCalendarId);
        var points = orderedStops
            .Where(stop => deliveries.TryGetValue(stop.DeliveryCalendarId, out var delivery) && delivery.HasCoordinates)
            .Select(stop => ToOptimizationPoint(deliveries[stop.DeliveryCalendarId]))
            .ToList();

        return Math.Round(NearestNeighborRouteOptimizer.CalculateRouteDistanceKm(points), 2);
    }

    private static void EnsureRouteCanBeModified(DeliveryRoute route)
    {
        if (route.Status is RouteStatus.InProgress or RouteStatus.Completed)
        {
            throw new InvalidOperationException("Nie mozna zmienic trasy, ktora jest juz w realizacji albo zostala zakonczona.");
        }
    }

    private static void ValidateStopSequence(
        IReadOnlyCollection<DeliveryRouteStop> existingStops,
        IReadOnlyCollection<UpdateDeliveryRouteStopRequest> requestedStops)
    {
        if (existingStops.Count != requestedStops.Count ||
            requestedStops.Select(stop => stop.StopId).Distinct().Count() != requestedStops.Count ||
            existingStops.Select(stop => stop.Id).Except(requestedStops.Select(stop => stop.StopId)).Any())
        {
            throw new InvalidOperationException("Lista przystankow trasy jest niekompletna.");
        }

        var expectedSequence = Enumerable.Range(1, requestedStops.Count);
        if (!requestedStops.Select(stop => stop.SequenceNumber).OrderBy(number => number).SequenceEqual(expectedSequence))
        {
            throw new InvalidOperationException("Kolejnosc przystankow musi zawierac kolejne numery od 1.");
        }
    }

    private static RouteStopDto MapStop(
        DeliveryRouteStop stop,
        IReadOnlyDictionary<int, LogisticsDeliveryCandidate> deliveries)
    {
        deliveries.TryGetValue(stop.DeliveryCalendarId, out var delivery);

        return new RouteStopDto
        {
            Id = stop.Id,
            DeliveryCalendarId = stop.DeliveryCalendarId,
            SequenceNumber = stop.SequenceNumber,
            FullAddress = delivery?.FullAddress ?? string.Empty,
            Latitude = delivery?.Latitude,
            Longitude = delivery?.Longitude,
            EstimatedLoadKg = delivery?.EstimatedLoadKg ?? 0,
            Status = stop.Status.ToString(),
            PlannedArrivalTime = stop.PlannedArrivalTime,
        };
    }

    private static RouteDeliveryCandidateDto MapCandidate(LogisticsDeliveryCandidate candidate)
    {
        return new RouteDeliveryCandidateDto
        {
            DeliveryCalendarId = candidate.DeliveryCalendarId,
            OrderId = candidate.OrderId,
            OrderNumber = candidate.OrderNumber,
            FullAddress = candidate.FullAddress,
            EstimatedLoadKg = candidate.EstimatedLoadKg,
        };
    }

    private static RouteOptimizationPoint ToOptimizationPoint(LogisticsDeliveryCandidate delivery)
    {
        return new RouteOptimizationPoint(
            delivery.DeliveryCalendarId,
            delivery.Latitude!.Value,
            delivery.Longitude!.Value);
    }

    private static double DistanceBetween(LogisticsDeliveryCandidate source, LogisticsDeliveryCandidate target)
    {
        return DistanceBetween(ToOptimizationPoint(source), ToOptimizationPoint(target));
    }

    private static double DistanceBetween(RouteOptimizationPoint source, RouteOptimizationPoint target)
    {
        return NearestNeighborRouteOptimizer.CalculateRouteDistanceKm(new[] { source, target });
    }

    private static DailyRouteGenerationResultDto Failed(string code, string message)
    {
        return new DailyRouteGenerationResultDto
        {
            Succeeded = false,
            Issues =
            {
                new RouteGenerationIssueDto
                {
                    Code = code,
                    Message = message,
                },
            },
        };
    }

    private sealed class RouteBatch
    {
        public RouteBatch(Vehicle vehicle)
        {
            Vehicle = vehicle;
        }

        public Vehicle Vehicle { get; }

        public List<LogisticsDeliveryCandidate> Deliveries { get; } = new();

        public List<LogisticsDeliveryCandidate> UnassignedOverflow { get; } = new();

        public decimal TotalLoadKg => Deliveries.Sum(d => d.EstimatedLoadKg);
    }

    private sealed record DriverDisplay(string FullName);

    private sealed record GeographicSplit(
        RouteBatch SourceBatch,
        List<LogisticsDeliveryCandidate> SourceDeliveries,
        List<LogisticsDeliveryCandidate> TargetDeliveries,
        double DistanceSavingKm);
}
