using AutoMapper;
using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class CustomerSupportService : ICustomerSupportService
{
    private const int DeliveryOptionLimit = 200;

    private readonly ITicketRepository ticketRepository;
    private readonly ITicketAttachmentRepository ticketAttachmentRepository;
    private readonly IUserRepository userRepository;
    private readonly IOrderRepository orderRepository;
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;
    private readonly IAddressRepository addressRepository;
    private readonly IDeliveryWindowRepository deliveryWindowRepository;
    private readonly IOrderDataProvider orderDataProvider;
    private readonly IDietDataProvider dietDataProvider;
    private readonly IPackingService packingService;
    private readonly IPackingIncidentService packingIncidentService;
    private readonly IDeliveryRouteRepository deliveryRouteRepository;
    private readonly IDeliveryIssueRepository deliveryIssueRepository;
    private readonly IDriverRepository driverRepository;
    private readonly IVehicleRepository vehicleRepository;
    private readonly IMapper mapper;

    public CustomerSupportService(
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository ticketAttachmentRepository,
        IUserRepository userRepository,
        IOrderRepository orderRepository,
        IDeliveryCalendarRepository deliveryCalendarRepository,
        IAddressRepository addressRepository,
        IDeliveryWindowRepository deliveryWindowRepository,
        IOrderDataProvider orderDataProvider,
        IDietDataProvider dietDataProvider,
        IPackingService packingService,
        IPackingIncidentService packingIncidentService,
        IDeliveryRouteRepository deliveryRouteRepository,
        IDeliveryIssueRepository deliveryIssueRepository,
        IDriverRepository driverRepository,
        IVehicleRepository vehicleRepository,
        IMapper mapper)
    {
        this.ticketRepository = ticketRepository;
        this.ticketAttachmentRepository = ticketAttachmentRepository;
        this.userRepository = userRepository;
        this.orderRepository = orderRepository;
        this.deliveryCalendarRepository = deliveryCalendarRepository;
        this.addressRepository = addressRepository;
        this.deliveryWindowRepository = deliveryWindowRepository;
        this.orderDataProvider = orderDataProvider;
        this.dietDataProvider = dietDataProvider;
        this.packingService = packingService;
        this.packingIncidentService = packingIncidentService;
        this.deliveryRouteRepository = deliveryRouteRepository;
        this.deliveryIssueRepository = deliveryIssueRepository;
        this.driverRepository = driverRepository;
        this.vehicleRepository = vehicleRepository;
        this.mapper = mapper;
    }

    public async Task<TicketPageDto> SearchTicketsAsync(TicketSearchRequest request)
    {
        var result = await ticketRepository.SearchAsync(new TicketSearchQuery(
            request.Search,
            request.Status,
            request.Priority,
            request.AssignedToUserId,
            request.UnassignedOnly,
            request.OpenOnly,
            request.QueueOrder,
            request.Page,
            request.PageSize));

        return new TicketPageDto
        {
            Items = result.Items.Select(MapTicketSearchRow).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<TicketDashboardSummaryDto> GetTicketDashboardSummaryAsync()
    {
        var today = DateTime.Today;
        var summary = await ticketRepository.GetDashboardSummaryAsync(
            new DateTimeOffset(today),
            new DateTimeOffset(today.AddDays(1)));

        return new TicketDashboardSummaryDto
        {
            WaitingCount = summary.WaitingCount,
            UnassignedCount = summary.UnassignedCount,
            HighPriorityCount = summary.HighPriorityCount,
            ClosedTodayCount = summary.ClosedTodayCount,
        };
    }

    public async Task<IEnumerable<TicketDto>> GetTicketsByClientIdAsync(int clientUserId)
    {
        var tickets = await ticketRepository.GetByClientIdAsync(clientUserId);
        return await MapTicketsAsync(tickets);
    }

    public async Task<IEnumerable<TicketDto>> GetTicketsByStatusAsync(TicketStatus status)
    {
        var tickets = await ticketRepository.GetByStatusAsync(status);
        return await MapTicketsAsync(tickets);
    }

    public async Task<TicketDto?> GetTicketByIdAsync(int id)
    {
        var ticket = await ticketRepository.GetByIdAsync(id);
        if (ticket is null)
        {
            return null;
        }

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<IReadOnlyDictionary<int, TicketOperationalContextDto>> GetOperationalContextsAsync(
        IEnumerable<TicketDto> tickets)
    {
        var linkedTickets = tickets
            .Where(ticket => ticket.OrderId.HasValue || ticket.DeliveryCalendarId.HasValue)
            .ToArray();

        if (linkedTickets.Length == 0)
        {
            return new Dictionary<int, TicketOperationalContextDto>();
        }

        var data = await LoadOperationalContextDataAsync(linkedTickets);
        var result = new Dictionary<int, TicketOperationalContextDto>();
        foreach (var ticket in linkedTickets)
        {
            result[ticket.Id] = await BuildOperationalContextAsync(ticket, data);
        }

        return result;
    }

    public async Task<IReadOnlyList<TicketDeliveryOptionDto>> GetDeliveryOptionsAsync(
        DateTime fromInclusive,
        DateTime toInclusive)
    {
        var from = fromInclusive.Date;
        var toExclusive = toInclusive.Date.AddDays(1);
        if (toExclusive <= from)
        {
            toExclusive = from.AddDays(1);
        }

        var rows = await deliveryCalendarRepository.SearchTicketDeliveryOptionsAsync(
            from,
            toExclusive,
            DeliveryOptionLimit);

        return rows
            .Select(row => new TicketDeliveryOptionDto
            {
                DeliveryCalendarId = row.DeliveryCalendarId,
                OrderId = row.OrderId,
                CustomerId = row.CustomerId,
                OrderNumber = row.OrderNumber,
                CustomerFullName = row.CustomerFullName,
                DeliveryDate = row.DeliveryDate,
                DeliveryStatus = row.DeliveryStatus.ToString(),
                AddressFullLine = row.AddressFullLine,
                DietSummary = row.DietSummary,
            })
            .ToArray();
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketRequest request)
    {
        var ticket = mapper.Map<Ticket>(request);
        await ApplyTicketOrderContextAsync(ticket);
        await EnsureUserExistsAsync(ticket.ClientUserId);

        var id = await ticketRepository.InsertAsync(ticket);
        ticket.Id = id;

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<TicketDto?> UpdateTicketAsync(int id, UpdateTicketRequest request)
    {
        var existing = await ticketRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        if (request.AssignedToUserId.HasValue)
        {
            await EnsureUserExistsAsync(request.AssignedToUserId.Value);
        }

        request.Id = id;
        mapper.Map(request, existing);
        await ApplyTicketOrderContextAsync(existing, overwriteClientFromOrder: false);
        ApplyClosedAt(existing);
        await ticketRepository.UpdateAsync(existing);

        return (await MapTicketsAsync(new[] { existing })).Single();
    }

    public async Task<TicketDto?> AssignTicketAsync(AssignTicketRequest request)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket is null)
        {
            return null;
        }

        await EnsureUserExistsAsync(request.AssignedToUserId);

        ticket.AssignedToUserId = request.AssignedToUserId;
        if (ticket.Status == TicketStatus.New)
        {
            ticket.Status = TicketStatus.Open;
        }

        ApplyClosedAt(ticket);
        await ticketRepository.UpdateAsync(ticket);

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<TicketDto?> ChangeTicketStatusAsync(ChangeTicketStatusRequest request)
    {
        var ticket = await ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket is null)
        {
            return null;
        }

        ticket.Status = request.Status;
        ApplyClosedAt(ticket);
        await ticketRepository.UpdateAsync(ticket);

        return (await MapTicketsAsync(new[] { ticket })).Single();
    }

    public async Task<bool> DeleteTicketAsync(int id)
    {
        return await ticketRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<TicketAttachmentDto>> GetTicketAttachmentsAsync(int ticketId)
    {
        var attachments = await ticketAttachmentRepository.GetByTicketIdAsync(ticketId);
        return await MapTicketAttachmentsAsync(attachments);
    }

    public async Task<TicketAttachmentDto?> GetTicketAttachmentByIdAsync(int id)
    {
        var attachment = await ticketAttachmentRepository.GetByIdAsync(id);
        if (attachment is null)
        {
            return null;
        }

        return (await MapTicketAttachmentsAsync(new[] { attachment })).Single();
    }

    public async Task<TicketAttachmentDto> AddTicketAttachmentAsync(CreateTicketAttachmentRequest request)
    {
        await EnsureTicketExistsAsync(request.TicketId);
        await EnsureUserExistsAsync(request.UploadedByUserId);

        var attachment = mapper.Map<TicketAttachment>(request);
        var id = await ticketAttachmentRepository.InsertAsync(attachment);
        attachment.Id = id;

        return (await MapTicketAttachmentsAsync(new[] { attachment })).Single();
    }

    public async Task<bool> DeleteTicketAttachmentAsync(int id)
    {
        return await ticketAttachmentRepository.DeleteAsync(id);
    }

    private async Task<TicketOperationalContextData> LoadOperationalContextDataAsync(
        IReadOnlyCollection<TicketDto> tickets)
    {
        var explicitDeliveryIds = tickets
            .Select(ticket => ticket.DeliveryCalendarId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var explicitDeliveries = await deliveryCalendarRepository.GetByIdsAsync(explicitDeliveryIds);
        var orderIds = tickets
            .Select(ticket => ticket.OrderId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Concat(explicitDeliveries.Select(delivery => delivery.OrderId))
            .Distinct()
            .ToArray();
        var ordersById = await orderRepository.GetWithItemsAndDeliveryByIdsAsync(orderIds);
        var deliveriesById = explicitDeliveries
            .Concat(ordersById.Values.SelectMany(order => order.DeliveryDays))
            .GroupBy(delivery => delivery.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var deliveries = deliveriesById.Values.ToArray();
        var addresses = await addressRepository.GetByIdsAsync(deliveries.Select(delivery => delivery.AddressId));
        var windows = await deliveryWindowRepository.GetByIdsAsync(
            deliveries
                .Select(delivery => delivery.DeliveryWindowId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value));

        return new TicketOperationalContextData
        {
            DeliveriesById = deliveriesById,
            OrdersById = ordersById,
            AddressesById = addresses.ToDictionary(address => address.Id),
            DeliveryWindowsById = windows.ToDictionary(window => window.Id),
            DeliveryInfosByDeliveryId = await LoadDeliveryInfosAsync(deliveries),
            RoutesByDeliveryId = await LoadRouteContextsAsync(deliveries),
            PackingBagsByDeliveryId = await LoadPackingBagsAsync(deliveries),
            PackingIncidentsByDeliveryId = await LoadPackingIncidentsAsync(deliveries),
        };
    }

    private async Task<IReadOnlyDictionary<int, OrderDeliveryInfo>> LoadDeliveryInfosAsync(
        IReadOnlyCollection<DeliveryCalendar> deliveries)
    {
        var deliveryIds = deliveries.Select(delivery => delivery.Id).ToHashSet();
        if (deliveryIds.Count == 0)
        {
            return new Dictionary<int, OrderDeliveryInfo>();
        }

        var result = new Dictionary<int, OrderDeliveryInfo>();
        foreach (var deliveryDate in deliveries.Select(delivery => delivery.DeliveryDate.Date).Distinct())
        {
            try
            {
                var dayDeliveries = await orderDataProvider.GetDeliveriesForDateAsync(deliveryDate);
                foreach (var deliveryInfo in dayDeliveries)
                {
                    if (deliveryIds.Contains(deliveryInfo.DeliveryCalendarId))
                    {
                        result[deliveryInfo.DeliveryCalendarId] = deliveryInfo;
                    }
                }
            }
            catch
            {
                // M1 is supportive context for BOK; ticket list must stay available if it is temporarily unavailable.
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<int, TicketRouteContextDto>> LoadRouteContextsAsync(
        IReadOnlyCollection<DeliveryCalendar> deliveries)
    {
        var deliveryIds = deliveries.Select(delivery => delivery.Id).ToHashSet();
        if (deliveryIds.Count == 0)
        {
            return new Dictionary<int, TicketRouteContextDto>();
        }

        var matches = new List<RouteStopMatch>();
        foreach (var deliveryDate in deliveries.Select(delivery => delivery.DeliveryDate.Date).Distinct())
        {
            var routes = await deliveryRouteRepository.GetRoutesWithStopsAsync(
                new DateTimeOffset(deliveryDate, TimeSpan.Zero));

            matches.AddRange(routes
                .SelectMany(route => route.Stops.Select(stop => new RouteStopMatch(route, stop)))
                .Where(match => deliveryIds.Contains(match.Stop.DeliveryCalendarId)));
        }

        if (matches.Count == 0)
        {
            return new Dictionary<int, TicketRouteContextDto>();
        }

        var drivers = (await driverRepository.GetByIdsAsync(matches
                .Select(match => match.Route.DriverId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)))
            .ToDictionary(driver => driver.Id);
        var driverUsers = (await userRepository.GetByIdsAsync(drivers.Values.Select(driver => driver.UserId)))
            .ToDictionary(user => user.Id);
        var vehicles = (await vehicleRepository.GetByIdsAsync(matches
                .Select(match => match.Route.VehicleId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)))
            .ToDictionary(vehicle => vehicle.Id);
        var issuesByStopId = (await deliveryIssueRepository.GetByRouteStopIdsAsync(matches.Select(match => match.Stop.Id)))
            .GroupBy(issue => issue.RouteStopId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var result = new Dictionary<int, TicketRouteContextDto>();
        foreach (var match in matches.GroupBy(item => item.Stop.DeliveryCalendarId).Select(group => group.First()))
        {
            string? driverName = null;
            if (match.Route.DriverId.HasValue &&
                drivers.TryGetValue(match.Route.DriverId.Value, out var driver) &&
                driverUsers.TryGetValue(driver.UserId, out var driverUser))
            {
                driverName = BuildUserFullName(driverUser);
            }

            var vehicle = match.Route.VehicleId.HasValue &&
                vehicles.TryGetValue(match.Route.VehicleId.Value, out var matchedVehicle)
                    ? matchedVehicle
                    : null;
            var latestIssue = issuesByStopId.TryGetValue(match.Stop.Id, out var issues)
                ? issues.OrderByDescending(issue => issue.ReportedAt).FirstOrDefault()
                : null;

            result[match.Stop.DeliveryCalendarId] = new TicketRouteContextDto
            {
                RouteId = match.Route.Id,
                RouteName = match.Route.Name,
                StopId = match.Stop.Id,
                SequenceNumber = match.Stop.SequenceNumber,
                StopStatus = match.Stop.Status.ToString(),
                PlannedArrivalTime = match.Stop.PlannedArrivalTime,
                ActualArrivalTime = match.Stop.ActualArrivalTime,
                DriverName = driverName,
                VehicleRegistration = vehicle?.RegistrationNumber,
                VehicleModel = vehicle?.Model,
                LatestIssue = latestIssue is null
                    ? null
                    : new TicketDeliveryIssueContextDto
                    {
                        Reason = latestIssue.Reason,
                        Notes = latestIssue.Notes,
                        ReportedAt = latestIssue.ReportedAt,
                    },
            };
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<TicketPackingBagContextDto>>> LoadPackingBagsAsync(
        IReadOnlyCollection<DeliveryCalendar> deliveries)
    {
        var deliveryIds = deliveries.Select(delivery => delivery.Id).ToHashSet();
        var result = new Dictionary<int, List<TicketPackingBagContextDto>>();
        if (deliveryIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<TicketPackingBagContextDto>>();
        }

        foreach (var deliveryDate in deliveries.Select(delivery => DateOnly.FromDateTime(delivery.DeliveryDate)).Distinct())
        {
            try
            {
                var board = await packingService.GetPackingBoardAsync(deliveryDate);
                foreach (var group in board.Routes
                    .SelectMany(route => route.Bags)
                    .Where(bag => bag.DeliveryCalendarId.HasValue && deliveryIds.Contains(bag.DeliveryCalendarId.Value))
                    .GroupBy(bag => bag.DeliveryCalendarId!.Value))
                {
                    if (!result.TryGetValue(group.Key, out var contexts))
                    {
                        contexts = new List<TicketPackingBagContextDto>();
                        result[group.Key] = contexts;
                    }

                    contexts.AddRange(group
                        .OrderBy(bag => bag.BagNumber)
                        .Select(bag => new TicketPackingBagContextDto
                        {
                            PackingBagId = bag.PackingBagId,
                            BagCode = bag.BagCode,
                            BagNumber = bag.BagNumber,
                            StatusText = string.IsNullOrWhiteSpace(bag.StatusText) ? bag.Status : bag.StatusText,
                            TotalBoxes = bag.TotalBoxes,
                            PackedBoxes = bag.PackedBoxes,
                            HasLabels = bag.HasLabels,
                            IsTransportLabelAttached = bag.IsTransportLabelAttached,
                            TransportLabelAttachedAt = bag.TransportLabelAttachedAt,
                        }));
                }
            }
            catch
            {
                // Packing context is auxiliary; do not fail ticket listing when M5 data is incomplete.
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<TicketPackingBagContextDto>)pair.Value
                .OrderBy(bag => bag.BagNumber)
                .ToArray());
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<TicketPackingIncidentContextDto>>> LoadPackingIncidentsAsync(
        IReadOnlyCollection<DeliveryCalendar> deliveries)
    {
        var deliveryIds = deliveries.Select(delivery => delivery.Id).ToArray();
        if (deliveryIds.Length == 0)
        {
            return new Dictionary<int, IReadOnlyList<TicketPackingIncidentContextDto>>();
        }

        var incidents = await packingIncidentService.SearchByDeliveryCalendarIdsAsync(deliveryIds);
        return incidents
            .Where(incident => incident.DeliveryCalendarId.HasValue)
            .GroupBy(incident => incident.DeliveryCalendarId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TicketPackingIncidentContextDto>)group
                    .OrderByDescending(incident => incident.ReportedAt)
                    .Select(MapPackingIncidentContext)
                    .ToArray());
    }

    private async Task<TicketOperationalContextDto> BuildOperationalContextAsync(TicketDto ticket)
    {
        var data = await LoadOperationalContextDataAsync(new[] { ticket });
        return await BuildOperationalContextAsync(ticket, data);
    }

    private async Task<TicketOperationalContextDto> BuildOperationalContextAsync(
        TicketDto ticket,
        TicketOperationalContextData data)
    {
        var hints = new List<string>();
        var context = new TicketOperationalContextDto
        {
            TicketId = ticket.Id,
            OrderId = ticket.OrderId,
            DeliveryCalendarId = ticket.DeliveryCalendarId,
        };

        DeliveryCalendar? delivery = null;
        if (ticket.DeliveryCalendarId.HasValue)
        {
            data.DeliveriesById.TryGetValue(ticket.DeliveryCalendarId.Value, out delivery);
            if (delivery is null)
            {
                hints.Add($"Nie znaleziono dostawy #{ticket.DeliveryCalendarId.Value}.");
            }
            else
            {
                context.DeliveryCalendarId = delivery.Id;
                context.OrderId = delivery.OrderId;
            }
        }

        Order? order = null;
        if (context.OrderId.HasValue)
        {
            data.OrdersById.TryGetValue(context.OrderId.Value, out order);
            if (order is null)
            {
                hints.Add($"Nie znaleziono zamowienia #{context.OrderId.Value}.");
            }
        }

        if (delivery is null && order?.DeliveryDays.Count == 1)
        {
            delivery = order.DeliveryDays[0];
            context.DeliveryCalendarId = delivery.Id;
        }

        var deliveryInfo = delivery is null
            ? null
            : data.DeliveryInfosByDeliveryId.GetValueOrDefault(delivery.Id);

        if (order is not null)
        {
            context.OrderId = order.Id;
            context.OrderNumber = order.OrderNumber;
            context.OrderStatus = order.Status.ToString();
            context.OrderStartDate = order.StartDate;
            context.OrderEndDate = order.EndDate;
            context.FinalPrice = order.FinalPrice;

            if (order.CustomerId != ticket.ClientUserId)
            {
                hints.Add("Klient w zgloszeniu rozni sie od klienta przypisanego do zamowienia.");
            }
        }

        if (delivery is not null)
        {
            data.AddressesById.TryGetValue(delivery.AddressId, out var address);
            var window = delivery.DeliveryWindowId.HasValue &&
                data.DeliveryWindowsById.TryGetValue(delivery.DeliveryWindowId.Value, out var matchedWindow)
                    ? matchedWindow
                    : null;

            context.DeliveryCalendarId = delivery.Id;
            context.DeliveryDate = delivery.DeliveryDate;
            context.DeliveryStatus = delivery.Status.ToString();
            context.IsSkipped = delivery.IsSkipped;
            context.SkipReason = delivery.SkipReason;
            context.CutoffTime = delivery.CutoffTime;
            context.DeliveryWindowName = deliveryInfo?.DeliveryWindowName
                ?? window?.Name;
            context.DeliveryWindowRange = window is null
                ? null
                : $"{window.StartTime}-{window.EndTime}";
            context.AddressFullLine = deliveryInfo?.AddressFullLine
                ?? address?.FullAddress;
            context.City = deliveryInfo?.City
                ?? address?.City;
            context.PostalCode = deliveryInfo?.PostalCode
                ?? address?.PostalCode;
            context.DeliveryNotes = address?.DeliveryNotes;
            context.ClientPublicId = deliveryInfo?.ClientPublicId;
            context.Route = data.RoutesByDeliveryId.GetValueOrDefault(delivery.Id);
            context.PackingBags = data.PackingBagsByDeliveryId.GetValueOrDefault(delivery.Id) ?? [];
            context.PackingIncidents = data.PackingIncidentsByDeliveryId.GetValueOrDefault(delivery.Id) ?? [];
        }

        var orderItems = BuildOrderItems(order, deliveryInfo).ToArray();
        context.OrderItems = orderItems;
        context.Meals = await BuildMealContextsAsync(
            delivery is null ? null : DateOnly.FromDateTime(delivery.DeliveryDate),
            orderItems);

        context.AssessmentHints = BuildAssessmentHints(context, hints);
        return context;
    }

    private async Task<IReadOnlyList<TicketMealContextDto>> BuildMealContextsAsync(
        DateOnly? deliveryDate,
        IReadOnlyList<TicketOrderItemContextDto> orderItems)
    {
        if (!deliveryDate.HasValue || orderItems.Count == 0)
        {
            return [];
        }

        PublishedDietPlanSnapshotDto? snapshot = null;
        IReadOnlyList<DietPlanEntry> planEntries = [];

        try
        {
            snapshot = await dietDataProvider.GetPublishedPlanSnapshotAsync(deliveryDate.Value);
            if (snapshot is null)
            {
                planEntries = (await dietDataProvider.GetPlanForDateAsync(deliveryDate.Value)).ToArray();
            }
        }
        catch
        {
            planEntries = [];
        }

        var mealContexts = new List<TicketMealContextDto>();
        var recipeCache = new Dictionary<int, IReadOnlyList<RecipeIngredientEntry>>();
        var detailsCache = new Dictionary<int, MealCookingDetailsEntry?>();

        foreach (var orderItem in orderItems)
        {
            var context = new TicketMealContextDto
            {
                DietName = orderItem.DietName,
                VariantName = orderItem.VariantName,
                MealSlot = orderItem.MealSlot,
                MealId = orderItem.MealId,
                DietMenuPlanItemId = orderItem.DietMenuPlanItemId,
                MealName = string.Empty,
            };

            var snapshotItem = snapshot is null
                ? null
                : FindSnapshotItem(orderItem, snapshot.Items);

            if (snapshotItem is not null)
            {
                ApplySnapshotItem(context, snapshotItem);
            }
            else
            {
                var planEntry = FindPlanEntry(orderItem, planEntries);
                if (planEntry is not null)
                {
                    context.MealId = planEntry.MealId;
                    context.DietMenuPlanItemId = planEntry.DietMenuPlanItemId;
                    context.MealName = planEntry.MealName;
                    context.CategoryName = planEntry.CategoryName;
                    context.MealSlot = string.IsNullOrWhiteSpace(orderItem.MealSlot)
                        ? planEntry.MealSlot
                        : orderItem.MealSlot;
                }
            }

            if (context.MealId.HasValue && context.Ingredients.Count == 0)
            {
                var mealId = context.MealId.Value;
                if (!recipeCache.TryGetValue(mealId, out var recipe))
                {
                    recipe = (await dietDataProvider.GetRecipeForMealAsync(mealId)).ToArray();
                    recipeCache[mealId] = recipe;
                }

                context.Ingredients = recipe
                    .Select(item => item.IngredientName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToArray();
            }

            if (context.MealId.HasValue && string.IsNullOrWhiteSpace(context.MealName))
            {
                var mealId = context.MealId.Value;
                if (!detailsCache.TryGetValue(mealId, out var details))
                {
                    details = await dietDataProvider.GetMealCookingDetailsAsync(mealId);
                    detailsCache[mealId] = details;
                }

                if (details is not null)
                {
                    context.MealName = details.MealName;
                    context.CategoryName ??= details.CategoryName;
                    context.Allergens = MergeDistinct(context.Allergens, details.Allergens);
                    context.NutritionSummary ??= BuildNutritionSummary(details.NutritionFacts);
                }
            }

            if (string.IsNullOrWhiteSpace(context.MealName))
            {
                context.MealName = $"{orderItem.DietName} {orderItem.VariantName}".Trim();
            }

            mealContexts.Add(context);
        }

        return mealContexts;
    }

    private async Task ApplyTicketOrderContextAsync(
        Ticket ticket,
        bool overwriteClientFromOrder = true)
    {
        if (ticket.DeliveryCalendarId.HasValue)
        {
            var delivery = await deliveryCalendarRepository.GetByIdAsync(ticket.DeliveryCalendarId.Value)
                ?? throw new InvalidOperationException($"Dostawa o ID {ticket.DeliveryCalendarId.Value} nie istnieje.");

            ticket.OrderId = delivery.OrderId;
        }

        if (ticket.OrderId.HasValue)
        {
            var order = await orderRepository.GetByIdAsync(ticket.OrderId.Value)
                ?? throw new InvalidOperationException($"Zamowienie o ID {ticket.OrderId.Value} nie istnieje.");

            if (overwriteClientFromOrder || ticket.ClientUserId <= 0)
            {
                ticket.ClientUserId = order.CustomerId;
            }
            else if (ticket.ClientUserId != order.CustomerId)
            {
                throw new InvalidOperationException("Wybrane zamowienie nalezy do innego klienta niz zgloszenie.");
            }
        }
    }

    private async Task<List<TicketDto>> MapTicketsAsync(IEnumerable<Ticket> tickets)
    {
        var list = mapper.Map<List<TicketDto>>(tickets);
        var userIds = list
            .Select(ticket => ticket.ClientUserId)
            .Concat(list
                .Select(ticket => ticket.AssignedToUserId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value))
            .Distinct()
            .ToArray();
        var users = (await userRepository.GetByIdsAsync(userIds)).ToDictionary(user => user.Id);

        foreach (var ticket in list)
        {
            if (users.TryGetValue(ticket.ClientUserId, out var client))
            {
                ticket.ClientFullName = BuildUserFullName(client);
            }

            if (ticket.AssignedToUserId.HasValue &&
                users.TryGetValue(ticket.AssignedToUserId.Value, out var assigned))
            {
                ticket.AssignedToFullName = BuildUserFullName(assigned);
            }
        }

        return list;
    }

    private static TicketDto MapTicketSearchRow(TicketSearchRow row)
        => new()
        {
            Id = row.Id,
            Title = row.Title,
            Description = row.Description,
            ClientUserId = row.ClientUserId,
            ClientFullName = row.ClientFullName,
            OrderId = row.OrderId,
            DeliveryCalendarId = row.DeliveryCalendarId,
            AssignedToUserId = row.AssignedToUserId,
            AssignedToFullName = row.AssignedToFullName,
            Status = row.Status.ToString(),
            Priority = row.Priority.ToString(),
            ClosedAt = row.ClosedAt,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };

    private async Task<List<TicketAttachmentDto>> MapTicketAttachmentsAsync(
        IEnumerable<TicketAttachment> attachments)
    {
        var list = mapper.Map<List<TicketAttachmentDto>>(attachments);
        var users = (await userRepository.GetByIdsAsync(list.Select(attachment => attachment.UploadedByUserId)))
            .ToDictionary(user => user.Id);

        foreach (var attachment in list)
        {
            if (users.TryGetValue(attachment.UploadedByUserId, out var user))
            {
                attachment.UploadedByFullName = BuildUserFullName(user);
            }
        }

        return list;
    }

    private async Task EnsureTicketExistsAsync(int ticketId)
    {
        if (await ticketRepository.GetByIdAsync(ticketId) is null)
        {
            throw new InvalidOperationException($"Zgloszenie o ID {ticketId} nie istnieje.");
        }
    }

    private async Task EnsureUserExistsAsync(int userId)
    {
        if (await userRepository.GetByIdAsync(userId) is null)
        {
            throw new InvalidOperationException($"Uzytkownik o ID {userId} nie istnieje.");
        }
    }

    private static IReadOnlyList<TicketOrderItemContextDto> BuildOrderItems(
        Order? order,
        OrderDeliveryInfo? deliveryInfo)
    {
        var deliveryItems = deliveryInfo?.Items ?? Array.Empty<OrderItemInfo>();

        if (order?.Items.Count > 0)
        {
            return order.Items
                .OrderBy(item => item.Id)
                .Select(item =>
                {
                    var deliveryItem = FindDeliveryItem(item, deliveryItems);
                    return new TicketOrderItemContextDto
                    {
                        DietId = item.DietId,
                        DietVariantId = item.DietVariantId,
                        DietName = item.DietName,
                        VariantName = item.VariantName,
                        CaloriesPerDay = item.CaloriesPerDay,
                        TotalDays = item.TotalDays,
                        MealId = item.MealId ?? deliveryItem?.MealId,
                        MealVariantId = item.MealVariantId ?? deliveryItem?.MealVariantId,
                        DietMenuPlanItemId = item.DietMenuPlanItemId ?? deliveryItem?.DietMenuPlanItemId,
                        MealSlot = item.MealSlot ?? deliveryItem?.MealSlot,
                    };
                })
                .ToArray();
        }

        return deliveryItems
            .Select(item => new TicketOrderItemContextDto
            {
                DietId = item.DietId,
                DietVariantId = item.DietVariantId,
                DietName = item.DietName,
                VariantName = item.VariantName,
                CaloriesPerDay = item.CaloriesPerDay,
                MealId = item.MealId,
                MealVariantId = item.MealVariantId,
                DietMenuPlanItemId = item.DietMenuPlanItemId,
                MealSlot = item.MealSlot,
            })
            .ToArray();
    }

    private static OrderItemInfo? FindDeliveryItem(
        OrderItem orderItem,
        IReadOnlyList<OrderItemInfo> deliveryItems)
    {
        return deliveryItems.FirstOrDefault(item =>
                orderItem.DietMenuPlanItemId.HasValue &&
                item.DietMenuPlanItemId == orderItem.DietMenuPlanItemId)
            ?? deliveryItems.FirstOrDefault(item =>
                item.DietVariantId == orderItem.DietVariantId &&
                item.MealId == orderItem.MealId &&
                string.Equals(item.MealSlot, orderItem.MealSlot, StringComparison.OrdinalIgnoreCase))
            ?? deliveryItems.FirstOrDefault(item => item.DietVariantId == orderItem.DietVariantId);
    }

    private static PublishedDietPlanItemDto? FindSnapshotItem(
        TicketOrderItemContextDto orderItem,
        IReadOnlyList<PublishedDietPlanItemDto> snapshotItems)
    {
        return snapshotItems.FirstOrDefault(item =>
                orderItem.DietMenuPlanItemId.HasValue &&
                item.DietMenuPlanItemId == orderItem.DietMenuPlanItemId)
            ?? snapshotItems.FirstOrDefault(item =>
                item.DietVariantId == orderItem.DietVariantId &&
                orderItem.MealId.HasValue &&
                item.MealId == orderItem.MealId &&
                (string.IsNullOrWhiteSpace(orderItem.MealSlot) ||
                    string.Equals(item.MealSlot, orderItem.MealSlot, StringComparison.OrdinalIgnoreCase)))
            ?? snapshotItems.FirstOrDefault(item =>
                item.DietVariantId == orderItem.DietVariantId &&
                (string.IsNullOrWhiteSpace(orderItem.MealSlot) ||
                    string.Equals(item.MealSlot, orderItem.MealSlot, StringComparison.OrdinalIgnoreCase)));
    }

    private static DietPlanEntry? FindPlanEntry(
        TicketOrderItemContextDto orderItem,
        IReadOnlyList<DietPlanEntry> planEntries)
    {
        return planEntries.FirstOrDefault(item =>
                orderItem.DietMenuPlanItemId.HasValue &&
                item.DietMenuPlanItemId == orderItem.DietMenuPlanItemId)
            ?? planEntries.FirstOrDefault(item =>
                item.DietVariantId == orderItem.DietVariantId &&
                orderItem.MealId.HasValue &&
                item.MealId == orderItem.MealId &&
                (string.IsNullOrWhiteSpace(orderItem.MealSlot) ||
                    string.Equals(item.MealSlot, orderItem.MealSlot, StringComparison.OrdinalIgnoreCase)))
            ?? planEntries.FirstOrDefault(item =>
                item.DietVariantId == orderItem.DietVariantId &&
                (string.IsNullOrWhiteSpace(orderItem.MealSlot) ||
                    string.Equals(item.MealSlot, orderItem.MealSlot, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ApplySnapshotItem(
        TicketMealContextDto context,
        PublishedDietPlanItemDto snapshotItem)
    {
        context.MealId = snapshotItem.MealId;
        context.DietMenuPlanItemId = snapshotItem.DietMenuPlanItemId;
        context.MealName = snapshotItem.MealName;
        context.CategoryName = snapshotItem.CategoryName;
        context.MealSlot = string.IsNullOrWhiteSpace(context.MealSlot)
            ? snapshotItem.MealSlot
            : context.MealSlot;
        context.Allergens = MergeDistinct(
            snapshotItem.Allergens,
            snapshotItem.Components.SelectMany(component => component.Allergens));
        context.Ingredients = snapshotItem.Components
            .SelectMany(component => component.Ingredients)
            .Select(ingredient => ingredient.IngredientName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
        context.NutritionSummary = BuildNutritionSummary(snapshotItem.Nutrition);
        context.ValidationWarnings = MergeDistinct(
            snapshotItem.ValidationWarnings,
            snapshotItem.Components.SelectMany(component => component.ValidationWarnings));
    }

    private static IReadOnlyList<string> BuildAssessmentHints(
        TicketOperationalContextDto context,
        IReadOnlyList<string> existingHints)
    {
        var hints = new List<string>(existingHints);

        if (!context.OrderId.HasValue && !context.DeliveryCalendarId.HasValue)
        {
            hints.Add("Zgloszenie nie jest jeszcze powiazane z zamowieniem ani dostawa.");
        }

        if (context.IsSkipped)
        {
            hints.Add(string.IsNullOrWhiteSpace(context.SkipReason)
                ? "Dostawa jest oznaczona jako pominieta."
                : $"Dostawa jest oznaczona jako pominieta: {context.SkipReason}");
        }

        if (context.Route?.LatestIssue is not null)
        {
            hints.Add($"Kierowca zglosil problem: {context.Route.LatestIssue.Reason}.");
        }

        var openIncidents = context.PackingIncidents
            .Where(incident => !string.Equals(incident.Status, nameof(PackingIncidentStatus.Resolved), StringComparison.Ordinal))
            .ToArray();
        if (openIncidents.Length > 0)
        {
            hints.Add($"Sa otwarte zgloszenia kompletacji: {openIncidents.Length}.");
        }

        var allergens = context.Meals
            .SelectMany(meal => meal.Allergens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allergens.Length > 0)
        {
            hints.Add($"Alergeny w dostawie: {string.Join(", ", allergens.Take(8))}.");
        }

        if (context.OrderItems.Count > 0 &&
            context.OrderItems.All(item => !item.MealId.HasValue && !item.DietMenuPlanItemId.HasValue))
        {
            hints.Add("Pozycje zamowienia nie maja pelnego powiazania z planem menu M2.");
        }

        return hints
            .Where(hint => !string.IsNullOrWhiteSpace(hint))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static TicketPackingIncidentContextDto MapPackingIncidentContext(PackingIncidentDto incident)
        => new()
        {
            Id = incident.Id,
            Type = incident.Type.ToString(),
            Status = incident.Status.ToString(),
            ReasonSummary = incident.ReasonSummary,
            Description = incident.Description,
            MealName = incident.MealName,
            BoxCode = incident.BoxCode,
            BagCode = incident.BagCode,
            ReportedAt = incident.ReportedAt,
            ResolvedAt = incident.ResolvedAt,
        };

    private static IReadOnlyList<string> MergeDistinct(
        IEnumerable<string> first,
        IEnumerable<string> second)
    {
        return first
            .Concat(second)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? BuildNutritionSummary(LabelNutritionDto? nutrition)
    {
        if (nutrition is null)
        {
            return null;
        }

        var calories = nutrition.CaloriesPerServing ?? nutrition.CaloriesPer100g;
        var protein = nutrition.ProteinPerServing ?? nutrition.ProteinPer100g;
        var carbohydrates = nutrition.CarbohydratesPerServing ?? nutrition.CarbohydratesPer100g;
        var fat = nutrition.FatPerServing ?? nutrition.FatPer100g;
        var unit = nutrition.CaloriesPerServing.HasValue ? "porcja" : "100 g";

        return $"{calories:0} kcal/{unit}, B {protein:0.#} g, W {carbohydrates:0.#} g, T {fat:0.#} g";
    }

    private static string? BuildNutritionSummary(NutritionFactsEntry? nutrition)
    {
        if (nutrition is null)
        {
            return null;
        }

        return $"{nutrition.CaloriesPer100g:0} kcal/100 g, B {nutrition.ProteinPer100g:0.#} g, W {nutrition.CarbohydratesPer100g:0.#} g, T {nutrition.FatPer100g:0.#} g";
    }

    private static void ApplyClosedAt(Ticket ticket)
    {
        ticket.ClosedAt = ticket.Status is TicketStatus.Resolved or TicketStatus.Closed
            ? ticket.ClosedAt ?? DateTimeOffset.UtcNow
            : null;
    }

    private static string BuildUserFullName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private sealed record RouteStopMatch(DeliveryRoute Route, DeliveryRouteStop Stop);

    private sealed class TicketOperationalContextData
    {
        public IReadOnlyDictionary<int, DeliveryCalendar> DeliveriesById { get; init; }
            = new Dictionary<int, DeliveryCalendar>();

        public IReadOnlyDictionary<int, Order> OrdersById { get; init; }
            = new Dictionary<int, Order>();

        public IReadOnlyDictionary<int, Address> AddressesById { get; init; }
            = new Dictionary<int, Address>();

        public IReadOnlyDictionary<int, DeliveryWindow> DeliveryWindowsById { get; init; }
            = new Dictionary<int, DeliveryWindow>();

        public IReadOnlyDictionary<int, OrderDeliveryInfo> DeliveryInfosByDeliveryId { get; init; }
            = new Dictionary<int, OrderDeliveryInfo>();

        public IReadOnlyDictionary<int, TicketRouteContextDto> RoutesByDeliveryId { get; init; }
            = new Dictionary<int, TicketRouteContextDto>();

        public IReadOnlyDictionary<int, IReadOnlyList<TicketPackingBagContextDto>> PackingBagsByDeliveryId { get; init; }
            = new Dictionary<int, IReadOnlyList<TicketPackingBagContextDto>>();

        public IReadOnlyDictionary<int, IReadOnlyList<TicketPackingIncidentContextDto>> PackingIncidentsByDeliveryId { get; init; }
            = new Dictionary<int, IReadOnlyList<TicketPackingIncidentContextDto>>();
    }
}
