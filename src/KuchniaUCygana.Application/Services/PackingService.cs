using AutoMapper;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KuchniaUCygana.Application.Services;

/// <summary>
/// Serwis kompletacji: jedna PackingSession reprezentuje jedna torbe zamowienia.
/// </summary>
public sealed class PackingService : IPackingService
{
    private readonly IPackingSessionRepository sessionRepository;
    private readonly IRepository<PackingItem> itemRepository;
    private readonly IRepository<PackingLabel> labelRepository;
    private readonly IRepository<PackingManifest> packingManifestRepository;
    private readonly IOrderDataProvider orderProvider;
    private readonly IDietDataProvider dietProvider;
    private readonly IDeliveryManifestProvider manifestProvider;
    private readonly IMapper mapper;
    private readonly ILogger<PackingService> logger;

    public PackingService(
        IPackingSessionRepository sessionRepository,
        IRepository<PackingItem> itemRepository,
        IRepository<PackingLabel> labelRepository,
        IRepository<PackingManifest> packingManifestRepository,
        IOrderDataProvider orderProvider,
        IDietDataProvider dietProvider,
        IDeliveryManifestProvider manifestProvider,
        IMapper mapper,
        ILogger<PackingService> logger)
    {
        this.sessionRepository = sessionRepository;
        this.itemRepository = itemRepository;
        this.labelRepository = labelRepository;
        this.packingManifestRepository = packingManifestRepository;
        this.orderProvider = orderProvider;
        this.dietProvider = dietProvider;
        this.manifestProvider = manifestProvider;
        this.mapper = mapper;
        this.logger = logger;
    }

    public async Task<PackingBoardDto> GetPackingBoardAsync(DateOnly date)
    {
        var activeOrders = (await orderProvider.GetActiveOrdersAsync(date))
            .GroupBy(o => o.OrderId)
            .Select(g => g.First())
            .OrderBy(o => o.OrderId)
            .ToList();

        var deliveries = (await orderProvider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .GroupBy(d => d.OrderId)
            .ToDictionary(g => g.Key, g => g.First());

        var routes = (await manifestProvider.GetRoutesForDateAsync(date))
            .OrderBy(r => r.RouteId)
            .ToList();

        EnsureFallbackRoute(routes, date);

        var assignments = AssignOrdersToRoutes(activeOrders, deliveries, routes);
        var existingSessions = (await sessionRepository.GetByDateWithItemsAsync(date))
            .Where(s => s.OrderId.HasValue)
            .ToDictionary(s => s.OrderId!.Value);

        foreach (var assignment in assignments)
        {
            if (existingSessions.TryGetValue(assignment.Order.OrderId, out var existing))
            {
                var shouldUpdateCalendar = existing.DeliveryCalendarId is null;
                var shouldUpdateClient = string.IsNullOrWhiteSpace(existing.ClientName);

                if (shouldUpdateCalendar || shouldUpdateClient)
                {
                    // TODO [Sprint 7.1.6]: DeliveryCalendarId powinien przychodzić z assignment.Stop
                    existing.DeliveryCalendarId ??= assignment.Stop.DeliveryCalendarId;
                    existing.ClientName = string.IsNullOrWhiteSpace(existing.ClientName)
                        ? assignment.Order.ClientName
                        : existing.ClientName;

                    await sessionRepository.UpdateAsync(existing);
                }

                continue;
            }

            var session = new PackingSession
            {
                PackingDate = date,
                OrderId = assignment.Order.OrderId,
                ClientName = assignment.Order.ClientName,
                // TODO [Sprint 7.1.6]: DeliveryCalendarId powinien przychodzić z assignment.Stop
                DeliveryCalendarId = assignment.Stop.DeliveryCalendarId,
                Status = PackingStatus.Pending,
            };

            var id = await sessionRepository.InsertAsync(session);
            session.Id = id;
            existingSessions[assignment.Order.OrderId] = session;
        }

        var sessions = (await sessionRepository.GetByDateWithItemsAsync(date))
            .Where(s => s.OrderId.HasValue)
            .ToDictionary(s => s.OrderId!.Value);

        var board = new PackingBoardDto { PackingDate = date };
        var latestManifestsByRoute = (await packingManifestRepository.GetAllAsync())
            .Where(m => m.PackingDate == date && m.RouteId.HasValue)
            .GroupBy(m => m.RouteId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.GeneratedAt).ThenByDescending(m => m.Id).First());

        foreach (var route in routes)
        {
            var routeDto = new PackingRouteDto
            {
                RouteId = route.RouteId,
                RouteName = route.RouteName,
                VehicleId = route.VehicleId,
                VehicleRegistration = route.VehicleRegistration,
            };

            foreach (var assignment in assignments.Where(a => a.Route.RouteId == route.RouteId))
            {
                if (!sessions.TryGetValue(assignment.Order.OrderId, out var session))
                {
                    continue;
                }

                var transportLabel = await sessionRepository.GetShippingLabelAsync(session.Id);
                routeDto.Bags.Add(CreateBagDto(session, assignment, transportLabel is not null));
            }

            routeDto.Bags = routeDto.Bags
                .OrderBy(b => b.StopNumber)
                .ThenBy(b => b.OrderId)
                .ToList();
            routeDto.TotalBags = routeDto.Bags.Count;
            routeDto.PackedBags = routeDto.Bags.Count(b => b.Status is nameof(PackingStatus.Packed) or nameof(PackingStatus.Labeled) or nameof(PackingStatus.Loaded) or nameof(PackingStatus.Dispatched));
            routeDto.LoadedBags = routeDto.Bags.Count(b => b.Status is nameof(PackingStatus.Loaded) or nameof(PackingStatus.Dispatched));
            routeDto.DispatchedBags = routeDto.Bags.Count(b => b.Status == nameof(PackingStatus.Dispatched));
            routeDto.AllBagsPacked = routeDto.TotalBags > 0 && routeDto.PackedBags == routeDto.TotalBags;
            routeDto.AllBagsLoaded = routeDto.TotalBags > 0 && routeDto.LoadedBags == routeDto.TotalBags;

            if (latestManifestsByRoute.TryGetValue(route.RouteId, out var manifest))
            {
                routeDto.HasManifest = true;
                routeDto.ManifestId = manifest.Id;
                routeDto.ManifestNumber = manifest.ManifestNumber;
                routeDto.ManifestGeneratedAt = manifest.GeneratedAt;
                routeDto.IsManifestVerified = manifest.IsVerified;
                routeDto.ManifestVerifiedAt = manifest.VerifiedAt;
            }

            routeDto.CanGenerateManifest = routeDto.AllBagsPacked;
            routeDto.CanVerifyManifest = routeDto.HasManifest && !routeDto.IsManifestVerified && routeDto.AllBagsPacked;
            routeDto.CanLoadBags = routeDto.IsManifestVerified;
            routeDto.CanDispatchDelivery = routeDto.IsManifestVerified && routeDto.AllBagsLoaded;

            board.Routes.Add(routeDto);
        }

        board.TotalBags = board.Routes.Sum(r => r.TotalBags);
        board.PackedBags = board.Routes.Sum(r => r.PackedBags);
        board.LoadedBags = board.Routes.Sum(r => r.LoadedBags);

        return board;
    }

    public async Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy)
    {
        await GetPackingBoardAsync(date);
        var firstSession = (await sessionRepository.GetByDateWithItemsAsync(date)).FirstOrDefault()
            ?? throw new InvalidOperationException($"Brak zamowien do pakowania na {date:dd.MM.yyyy}.");

        firstSession.PackedBy = string.IsNullOrWhiteSpace(firstSession.PackedBy)
            ? packedBy
            : firstSession.PackedBy;
        await sessionRepository.UpdateAsync(firstSession);

        return MapSession(firstSession);
    }

    public async Task<PackingItemDto> PackClientDietAsync(int sessionId, int orderId)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {sessionId} nie istnieje.");

        if (session.OrderId.HasValue && session.OrderId.Value != orderId)
        {
            throw new InvalidOperationException($"Torba #{sessionId} jest przypisana do zamowienia #{session.OrderId}.");
        }

        if (!session.OrderId.HasValue)
        {
            var order = await orderProvider.GetOrderByIdAsync(orderId)
                ?? throw new InvalidOperationException($"Zamowienie {orderId} nie istnieje.");

            session.OrderId = orderId;
            session.ClientName = order.ClientName;
            await sessionRepository.UpdateAsync(session);
        }

        var boxes = (await PrepareOrderBoxesAsync(sessionId)).ToList();
        foreach (var box in boxes.Where(b => b.Status != nameof(PackingItemStatus.Packed)))
        {
            await MarkBoxPackedAsync(box.Id, "System");
        }

        await PackOrderBagAsync(sessionId, "System");

        return boxes.First();
    }

    public async Task<IEnumerable<PackingItemDto>> PrepareOrderBoxesAsync(int packingSessionId)
    {
        var session = await sessionRepository.GetWithItemsAsync(packingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {packingSessionId} nie istnieje.");

        if (session.OrderId is null)
        {
            throw new InvalidOperationException($"Torba #{packingSessionId} nie jest przypisana do zamowienia.");
        }

        if (session.Items.Count > 0)
        {
            return mapper.Map<List<PackingItemDto>>(session.Items);
        }

        var boxDefinitions = (await BuildBoxDefinitionsAsync(session)).ToList();
        var created = new List<PackingItem>();
        var sequence = 1;

        foreach (var definition in boxDefinitions)
        {
            var item = new PackingItem
            {
                PackingSessionId = session.Id,
                MealId = definition.MealId,
                MealName = definition.MealName,
                DietVariantId = definition.DietVariantId,
                BoxCode = CreateBoxCode(session, sequence),
                Status = PackingItemStatus.Pending,
                IsDamaged = false,
            };

            var id = await itemRepository.InsertAsync(item);
            item.Id = id;
            created.Add(item);
            sequence++;
        }

        logger.LogInformation(
            "Przygotowano {Count} pudelek dla torby {SessionId} zamowienia {OrderId}",
            created.Count,
            session.Id,
            session.OrderId);

        return mapper.Map<List<PackingItemDto>>(created);
    }

    public async Task MarkBoxPackedAsync(int packingItemId, string packedBy)
    {
        var item = await itemRepository.GetByIdAsync(packingItemId)
            ?? throw new InvalidOperationException($"Pudelko {packingItemId} nie istnieje.");

        if (item.Status != PackingItemStatus.Packed)
        {
            item.Status = PackingItemStatus.Packed;
            item.PackedAt = DateTimeOffset.UtcNow;
            item.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? "System" : packedBy;
            await itemRepository.UpdateAsync(item);
        }

        var session = await sessionRepository.GetWithItemsAsync(item.PackingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {item.PackingSessionId} nie istnieje.");

        if (session.Items.Count > 0 && session.Items.All(i => i.Status == PackingItemStatus.Packed))
        {
            session.Status = PackingStatus.Packed;
            session.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? session.PackedBy : packedBy;
            await sessionRepository.UpdateAsync(session);
        }
    }

    public async Task PackOrderBagAsync(int packingSessionId, string packedBy)
    {
        var session = await sessionRepository.GetWithItemsAsync(packingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {packingSessionId} nie istnieje.");

        if (session.Status is PackingStatus.Loaded or PackingStatus.Dispatched)
        {
            throw new InvalidOperationException("Torba jest juz w procesie zaladunku albo wysylki.");
        }

        if (session.Items.Count == 0)
        {
            await PrepareOrderBoxesAsync(packingSessionId);
            session = await sessionRepository.GetWithItemsAsync(packingSessionId)
                ?? throw new InvalidOperationException($"Torba pakowania {packingSessionId} nie istnieje.");
        }

        if (session.Items.Any(i => i.Status != PackingItemStatus.Packed))
        {
            throw new InvalidOperationException("Najpierw spakuj wszystkie pudelka w torbie.");
        }

        session.Status = PackingStatus.Packed;
        session.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? session.PackedBy : packedBy;
        await sessionRepository.UpdateAsync(session);
    }

    public async Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsAsync(int sessionId)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {sessionId} nie istnieje.");

        if (session.Status != PackingStatus.Packed && session.Status != PackingStatus.Labeled && session.Status != PackingStatus.Loaded && session.Status != PackingStatus.Dispatched)
        {
            throw new InvalidOperationException("Najpierw spakuj torbe (wszystkie pudelka), a dopiero potem wydrukuj etykiete transportowa.");
        }

        var board = await GetPackingBoardAsync(session.PackingDate);
        var bag = board.Routes
            .SelectMany(r => r.Bags)
            .FirstOrDefault(b => b.PackingSessionId == sessionId);

        var shippingLabel = await sessionRepository.GetShippingLabelAsync(sessionId);

        if (shippingLabel is null)
        {
            shippingLabel = new PackingLabel
            {
                PackingSessionId = sessionId,
                LabelType = LabelType.Shipping,
                QrCode = CreateTransportCode(session),
                ClientName = session.ClientName,
                RouteInfo = bag is not null
                    ? $"{bag.RouteName}, auto {bag.VehicleRegistration}, stop {bag.StopNumber}"
                    : CreateRouteInfo(session),
                DeliveryWindow = bag?.DeliveryWindow,
            };

            var id = await labelRepository.InsertAsync(shippingLabel);
            shippingLabel.Id = id;
        }

        if (session.Status == PackingStatus.Packed)
        {
            session.Status = PackingStatus.Labeled;
            await sessionRepository.UpdateAsync(session);
        }

        logger.LogInformation("Wygenerowano lub odczytano etykiete transportowa dla torby {SessionId}", sessionId);

        return mapper.Map<List<PackingLabelDto>>(new[] { shippingLabel });
    }

    public Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId)
    {
        return GenerateTransportLabelsAsync(sessionId);
    }

    public async Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForDeliveryAsync(DateOnly date, int routeId)
    {
        var board = await GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);
        var labels = new List<PackingLabelDto>();

        foreach (var bag in route.Bags)
        {
            labels.AddRange(await GenerateTransportLabelsAsync(bag.PackingSessionId));
        }

        return labels;
    }

    public async Task<IEnumerable<PackingSessionDto>> GetSessionsByDateAsync(DateOnly date)
    {
        await GetPackingBoardAsync(date);
        var sessions = await sessionRepository.GetByDateWithItemsAsync(date);
        return sessions.Select(MapSession).ToList();
    }

    public async Task<PackingSessionDto?> GetSessionByIdAsync(int sessionId)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId);
        return session is null ? null : MapSession(session);
    }

    public async Task<PackingLabelDto> PrintFoilLabelAsync(int packingItemId, string operatorName)
    {
        var item = await itemRepository.GetByIdAsync(packingItemId)
            ?? throw new InvalidOperationException($"Pudelko #{packingItemId} nie istnieje.");

        var existingLabel = await sessionRepository.GetProductLabelAsync(packingItemId);

        if (existingLabel is null)
        {
            var ingredients = await sessionRepository.GetMealIngredientsAsync(item.MealId);
            var allergens = await sessionRepository.GetMealAllergensAsync(item.MealId);
            var calories = await sessionRepository.GetMealCaloriesAsync(item.MealId);

            existingLabel = new PackingLabel
            {
                PackingItemId = packingItemId,
                LabelType = LabelType.Product,
                QrCode = item.BoxCode ?? $"BOX-{item.Id:D6}",
                DishName = item.MealName,
                Allergens = allergens.Any() ? string.Join(", ", allergens) : "Brak",
                Kcal = calories
            };

            var labelId = await labelRepository.InsertAsync(existingLabel);
            existingLabel.Id = labelId;
        }

        if (item.Status == PackingItemStatus.Pending)
        {
            item.Status = PackingItemStatus.FoilPrinted;
            item.FoilPrintedAt = DateTimeOffset.UtcNow;
            item.PackedBy = operatorName;
            await itemRepository.UpdateAsync(item);
        }

        var dto = mapper.Map<PackingLabelDto>(existingLabel);
        var mealIngredients = await sessionRepository.GetMealIngredientsAsync(item.MealId);
        dto.Ingredients = mealIngredients.Any() ? string.Join(", ", mealIngredients) : "Brak danych";

        return dto;
    }

    public async Task PackBoxByCodeAsync(int sessionId, string barcode, string packedBy)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {sessionId} nie istnieje.");

        var item = session.Items.FirstOrDefault(i => string.Equals(i.BoxCode, barcode, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            item = session.Items.FirstOrDefault(i => i.BoxCode is not null && i.BoxCode.EndsWith(barcode, StringComparison.OrdinalIgnoreCase));
        }

        if (item is null)
        {
            throw new InvalidOperationException($"Blad: Pudelko o kodzie {barcode} nie nalezy do tej torby (jest niekompatybilne z zamowieniem #{session.OrderId})!");
        }

        if (item.Status == PackingItemStatus.Pending)
        {
            throw new InvalidOperationException($"Blad: Pudelko {barcode} nie zostalo jeszcze zafoliowane w kuchni.");
        }

        if (item.Status == PackingItemStatus.Packed)
        {
            throw new InvalidOperationException($"Pudelko {barcode} jest juz spakowane.");
        }

        await MarkBoxPackedAsync(item.Id, packedBy);
    }

    private PackingSessionDto MapSession(PackingSession session)
    {
        var dto = mapper.Map<PackingSessionDto>(session);
        dto.Items = mapper.Map<List<PackingItemDto>>(session.Items);
        return dto;
    }

    private async Task<IEnumerable<BoxDefinition>> BuildBoxDefinitionsAsync(PackingSession session)
    {
        var orderId = session.OrderId!.Value;
        var deliveries = await orderProvider.GetDeliveriesForDateAsync(session.PackingDate.ToDateTime(TimeOnly.MinValue));
        var delivery = deliveries.FirstOrDefault(d => d.OrderId == orderId);

        if (delivery is not null && delivery.Items.Count > 0)
        {
            return delivery.Items.Select(item => new BoxDefinition(
                item.DietVariantId,
                $"{item.DietName} {item.VariantName}".Trim(),
                item.DietVariantId));
        }

        var order = await orderProvider.GetOrderByIdAsync(orderId)
            ?? throw new InvalidOperationException($"Zamowienie {orderId} nie istnieje.");

        var dietPlan = (await dietProvider.GetPlanForDateAsync(session.PackingDate))
            .Where(p => p.DietVariantId == order.DietVariantId)
            .Take(5)
            .ToList();

        if (dietPlan.Count > 0)
        {
            return dietPlan.Select(p => new BoxDefinition(p.MealId, p.MealName, p.DietVariantId));
        }

        return new[]
        {
            new BoxDefinition(0, $"Zestaw dietetyczny wariant {order.DietVariantId}", order.DietVariantId),
        };
    }

    private static List<RouteAssignment> AssignOrdersToRoutes(
        IReadOnlyList<ActiveOrderEntry> orders,
        IReadOnlyDictionary<int, OrderDeliveryInfo> deliveries,
        IReadOnlyList<RouteEntry> routes)
    {
        var stops = routes
            .SelectMany(route => route.Stops
                .OrderBy(stop => stop.SequenceNumber)
                .Select(stop => new RouteStop(route, stop)))
            .ToList();

        if (stops.Count == 0)
        {
            EnsureFallbackRoute((List<RouteEntry>)routes, DateOnly.FromDateTime(DateTime.Today));
            stops = routes
                .SelectMany(route => route.Stops.Select(stop => new RouteStop(route, stop)))
                .ToList();
        }

        var assignments = new List<RouteAssignment>();

        for (var i = 0; i < orders.Count; i++)
        {
            var stop = i < stops.Count
                ? stops[i]
                : CreateOverflowStop(stops[^1], i - stops.Count + 1);

            deliveries.TryGetValue(orders[i].OrderId, out var delivery);
            assignments.Add(new RouteAssignment(
                orders[i],
                stop.Route,
                stop.Stop,
                delivery?.AddressFullLine ?? CreateFallbackAddress(orders[i].OrderId),
                delivery?.DeliveryWindowName ?? $"{stop.Stop.DeliveryWindowFrom}-{stop.Stop.DeliveryWindowTo}"));
        }

        return assignments;
    }

    private static PackingBagDto CreateBagDto(PackingSession session, RouteAssignment assignment, bool hasLabels)
    {
        var totalBoxes = session.Items.Count;
        var packedBoxes = session.Items.Count(i => i.Status == PackingItemStatus.Packed);
        var status = session.Status.ToString();

        return new PackingBagDto
        {
            PackingSessionId = session.Id,
            OrderId = session.OrderId ?? assignment.Order.OrderId,
            ClientName = session.ClientName ?? assignment.Order.ClientName,
            DietType = GetDietType(assignment.Order.DietVariantId),
            Address = assignment.Address,
            RouteId = assignment.Route.RouteId,
            RouteName = assignment.Route.RouteName,
            VehicleId = assignment.Route.VehicleId,
            VehicleRegistration = assignment.Route.VehicleRegistration,
            StopNumber = assignment.Stop.SequenceNumber,
            DeliveryWindow = assignment.DeliveryWindow,
            Status = status,
            StatusText = GetStatusText(session.Status, totalBoxes, packedBoxes),
            StatusColor = GetStatusColor(session.Status),
            TotalBoxes = totalBoxes,
            PackedBoxes = packedBoxes,
            HasLabels = hasLabels,
            CanPackBag = totalBoxes > 0
                && packedBoxes == totalBoxes
                && session.Status == PackingStatus.Pending,
            CanLoad = session.Status == PackingStatus.Labeled,
        };
    }

    private static void EnsureFallbackRoute(List<RouteEntry> routes, DateOnly date)
    {
        if (routes.Count == 0)
        {
            routes.Add(new RouteEntry
            {
                RouteId = 0,
                RouteName = "Bez trasy",
                VehicleId = 0,
                VehicleRegistration = "BRAK",
            });
        }

        if (routes.Any(r => r.Stops.Count > 0))
        {
            foreach (var route in routes.Where(r => r.Stops.Count == 0))
            {
                route.Stops.Add(new RouteStopEntry
                {
                    StopId = date.DayNumber + route.RouteId,
                    SequenceNumber = 1,
                    DeliveryWindowFrom = "08:00",
                    DeliveryWindowTo = "08:30",
                    DeliveryCalendarId = 0,
                });
            }

            return;
        }

        routes[0].Stops.Add(new RouteStopEntry
        {
            StopId = date.DayNumber,
            SequenceNumber = 1,
            DeliveryWindowFrom = "08:00",
            DeliveryWindowTo = "08:30",
            DeliveryCalendarId = 0,
        });
    }

    private static RouteStop CreateOverflowStop(RouteStop lastStop, int overflowIndex)
    {
        return new RouteStop(
            lastStop.Route,
            new RouteStopEntry
            {
                StopId = lastStop.Stop.StopId + overflowIndex,
                SequenceNumber = lastStop.Stop.SequenceNumber + overflowIndex,
                DeliveryWindowFrom = lastStop.Stop.DeliveryWindowFrom,
                DeliveryWindowTo = lastStop.Stop.DeliveryWindowTo,
                DeliveryCalendarId = lastStop.Stop.DeliveryCalendarId,
            });
    }

    private static PackingRouteDto GetRouteOrThrow(PackingBoardDto board, int routeId)
    {
        return board.Routes.FirstOrDefault(r => r.RouteId == routeId)
            ?? throw new InvalidOperationException($"Dostawa/trasa {routeId} nie istnieje dla dnia {board.PackingDate:dd.MM.yyyy}.");
    }

    private static string CreateTransportCode(PackingSession session)
    {
        return $"BAG-{session.Id:D6}";
    }

    private static string CreateRouteInfo(PackingSession session)
    {
        // TODO [Sprint 7.1.6]: Pobierać RouteId/StopNumber dynamicznie JOIN-em po DeliveryCalendarId
        return session.DeliveryCalendarId.HasValue
            ? $"Dostawa #{session.DeliveryCalendarId}"
            : "Brak przypisanej dostawy";
    }

    private static string CreateBoxCode(PackingSession session, int sequence)
    {
        var orderPart = session.OrderId?.ToString("D6") ?? session.Id.ToString("D6");
        return $"BOX-{orderPart}-{sequence:D2}";
    }

    private static string CreateFallbackAddress(int orderId)
    {
        return $"Warszawa, ul. Przykladowa {orderId % 100 + 1}";
    }

    private static string GetDietType(int dietVariantId)
    {
        return dietVariantId switch
        {
            1 => "Standard 2000 kcal",
            2 => "Slim 1500 kcal",
            3 => "Sport 2500 kcal",
            4 => "Keto 1800 kcal",
            5 => "Vege 1800 kcal",
            _ => $"Dieta wariant {dietVariantId}",
        };
    }

    private static string GetStatusText(PackingStatus status, int totalBoxes, int packedBoxes)
    {
        return status switch
        {
            PackingStatus.Pending when totalBoxes == 0 => "Oczekuje na pudelka",
            PackingStatus.Pending => "Pudelka do kompletacji",
            PackingStatus.Labeled => "Etykieta transportowa",
            PackingStatus.Packed => "Torba spakowana",
            PackingStatus.Loaded => "Zaladowana do auta",
            PackingStatus.Dispatched => "Wyslana",
            _ when totalBoxes > 0 => $"{packedBoxes}/{totalBoxes} pudelek",
            _ => status.ToString(),
        };
    }

    private static string GetStatusColor(PackingStatus status)
    {
        return status switch
        {
            PackingStatus.Pending => "secondary",
            PackingStatus.Labeled => "azure",
            PackingStatus.Packed => "success",
            PackingStatus.Loaded => "indigo",
            PackingStatus.Dispatched => "dark",
            _ => "secondary",
        };
    }

    private sealed record BoxDefinition(int MealId, string MealName, int DietVariantId);

    private sealed record RouteStop(RouteEntry Route, RouteStopEntry Stop);

    private sealed record RouteAssignment(
        ActiveOrderEntry Order,
        RouteEntry Route,
        RouteStopEntry Stop,
        string Address,
        string DeliveryWindow);
}
