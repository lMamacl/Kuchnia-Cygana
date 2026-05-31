using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

/// <summary>
/// Koordynuje generowanie tras dziennych dla dyspozytora M4.
/// </summary>
public sealed class RoutingService : IDeliveryRouteService
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IDeliveryRouteStopRepository _routeStopRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogisticsDeliveryDataProvider _deliveryDataProvider;
    private readonly IRouteOptimizer _routeOptimizer;

    public RoutingService(
        IDeliveryRouteRepository routeRepository,
        IDeliveryRouteStopRepository routeStopRepository,
        IVehicleRepository vehicleRepository,
        ILogisticsDeliveryDataProvider deliveryDataProvider,
        IRouteOptimizer routeOptimizer)
    {
        _routeRepository = routeRepository;
        _routeStopRepository = routeStopRepository;
        _vehicleRepository = vehicleRepository;
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
            var route = await CreateRouteFromBatchAsync(routeDate, batch, createdRoutes.Count + 1);
            createdRoutes.Add(route);
        }

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

    private async Task<DeliveryRoute> CreateRouteFromBatchAsync(
        DateTimeOffset routeDate,
        RouteBatch batch,
        int routeNumber)
    {
        var route = new DeliveryRoute
        {
            RouteDate = routeDate,
            Name = $"Trasa {routeNumber}",
            Status = RouteStatus.Assigned,
            VehicleId = batch.Vehicle.Id,
        };

        var routeId = await _routeRepository.InsertAsync(route);
        route.Id = routeId;

        var optimizedIds = await _routeOptimizer.OptimizeSequenceAsync(
            batch.Deliveries.Select(ToOptimizationPoint).ToList());

        var deliveriesById = batch.Deliveries.ToDictionary(d => d.DeliveryCalendarId);
        var orderedDeliveries = optimizedIds.Select(id => deliveriesById[id]).ToList();
        var pointsById = batch.Deliveries.ToDictionary(d => d.DeliveryCalendarId, ToOptimizationPoint);
        var orderedPoints = optimizedIds.Select(id => pointsById[id]).ToList();

        route.TotalDistanceKm = Math.Round(NearestNeighborRouteOptimizer.CalculateRouteDistanceKm(orderedPoints), 2);
        await _routeRepository.UpdateAsync(route);

        var sequence = 1;
        foreach (var delivery in orderedDeliveries)
        {
            var stop = new DeliveryRouteStop
            {
                RouteId = routeId,
                DeliveryCalendarId = delivery.DeliveryCalendarId,
                SequenceNumber = sequence++,
                Status = StopStatus.Assigned,
            };

            var stopId = await _routeStopRepository.InsertAsync(stop);
            stop.Id = stopId;
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

        return batches;
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

        var vehicles = (await _vehicleRepository.GetAllAsync()).ToDictionary(v => v.Id);

        return routeList
            .OrderBy(r => r.Name)
            .Select(route => new DeliveryRouteDto
            {
                Id = route.Id,
                Name = route.Name,
                RouteDate = route.RouteDate,
                TotalDistanceKm = route.TotalDistanceKm,
                Status = route.Status.ToString(),
                DriverId = route.DriverId,
                VehicleId = route.VehicleId,
                VehicleRegistration = route.VehicleId.HasValue && vehicles.TryGetValue(route.VehicleId.Value, out var vehicle)
                    ? vehicle.RegistrationNumber
                    : null,
                Stops = route.Stops
                    .OrderBy(s => s.SequenceNumber)
                    .Select(stop => MapStop(stop, deliveries))
                    .ToList(),
            })
            .ToList();
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
        var sourcePoint = ToOptimizationPoint(source);
        var targetPoint = ToOptimizationPoint(target);
        return NearestNeighborRouteOptimizer.CalculateRouteDistanceKm(new[] { sourcePoint, targetPoint });
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
}
