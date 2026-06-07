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
    private readonly IRepository<TicketAttachment> ticketAttachmentRepository;
    private readonly IRepository<User> userRepository;
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
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IMapper mapper;

    public CustomerSupportService(
        ITicketRepository ticketRepository,
        IRepository<TicketAttachment> ticketAttachmentRepository,
        IRepository<User> userRepository,
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
        IRepository<Vehicle> vehicleRepository,
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

    public async Task<IEnumerable<TicketDto>> GetTicketsAsync()
    {
        var tickets = await ticketRepository.GetAllAsync();
        return await MapTicketsAsync(tickets);
    }

    public async Task<IEnumerable<TicketDto>> GetOpenTicketsAsync()
    {
        var tickets = await ticketRepository.GetOpenTicketsAsync();
        return await MapTicketsAsync(tickets);
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

        var result = new Dictionary<int, TicketOperationalContextDto>();
        foreach (var ticket in linkedTickets)
        {
            result[ticket.Id] = await BuildOperationalContextAsync(ticket);
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

        var deliveries = (await deliveryCalendarRepository.GetByDateRangeAsync(from, toExclusive))
            .Take(DeliveryOptionLimit)
            .ToArray();

        if (deliveries.Length == 0)
        {
            return [];
        }

        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);
        var orderCache = new Dictionary<int, Order?>();
        var addressCache = new Dictionary<int, Address?>();
        var options = new List<TicketDeliveryOptionDto>();

        foreach (var delivery in deliveries)
        {
            var order = await GetOrderWithItemsCachedAsync(delivery.OrderId, orderCache);
            if (order is null)
            {
                continue;
            }

            var address = await GetAddressCachedAsync(delivery.AddressId, addressCache);
            users.TryGetValue(order.CustomerId, out var customer);

            options.Add(new TicketDeliveryOptionDto
            {
                DeliveryCalendarId = delivery.Id,
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                OrderNumber = order.OrderNumber,
                CustomerFullName = customer is null
                    ? $"Klient #{order.CustomerId}"
                    : BuildUserFullName(customer),
                DeliveryDate = delivery.DeliveryDate,
                DeliveryStatus = delivery.Status.ToString(),
                AddressFullLine = address?.FullAddress ?? $"Adres #{delivery.AddressId}",
                DietSummary = BuildDietSummary(order.Items),
            });
        }

        return options
            .OrderByDescending(option => option.DeliveryDate)
            .ThenBy(option => option.OrderNumber)
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
        var attachments = await ticketAttachmentRepository.GetAllAsync();
        return await MapTicketAttachmentsAsync(attachments
            .Where(attachment => attachment.TicketId == ticketId)
            .OrderByDescending(attachment => attachment.UploadedAt));
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

    private async Task<TicketOperationalContextDto> BuildOperationalContextAsync(TicketDto ticket)
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
            delivery = await deliveryCalendarRepository.GetByIdAsync(ticket.DeliveryCalendarId.Value);
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
            order = await orderRepository.GetWithItemsAndDeliveryAsync(context.OrderId.Value);
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

        var deliveryInfo = await TryGetDeliveryInfoAsync(delivery);

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
            var address = await addressRepository.GetByIdAsync(delivery.AddressId);
            var window = delivery.DeliveryWindowId.HasValue
                ? await deliveryWindowRepository.GetByIdAsync(delivery.DeliveryWindowId.Value)
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
            context.Route = await BuildRouteContextAsync(delivery);
            context.PackingBags = await BuildPackingBagsAsync(delivery);
            context.PackingIncidents = await BuildPackingIncidentsAsync(delivery);
        }

        var orderItems = BuildOrderItems(order, deliveryInfo).ToArray();
        context.OrderItems = orderItems;
        context.Meals = await BuildMealContextsAsync(
            delivery is null ? null : DateOnly.FromDateTime(delivery.DeliveryDate),
            orderItems);

        context.AssessmentHints = BuildAssessmentHints(context, hints);
        return context;
    }

    private async Task<OrderDeliveryInfo?> TryGetDeliveryInfoAsync(DeliveryCalendar? delivery)
    {
        if (delivery is null)
        {
            return null;
        }

        try
        {
            var deliveries = await orderDataProvider.GetDeliveriesForDateAsync(delivery.DeliveryDate.Date);
            return deliveries.FirstOrDefault(item => item.DeliveryCalendarId == delivery.Id);
        }
        catch
        {
            return null;
        }
    }

    private async Task<TicketRouteContextDto?> BuildRouteContextAsync(DeliveryCalendar delivery)
    {
        var routes = await deliveryRouteRepository.GetRoutesWithStopsAsync(
            new DateTimeOffset(delivery.DeliveryDate.Date, TimeSpan.Zero));

        var match = routes
            .SelectMany(route => route.Stops.Select(stop => new { Route = route, Stop = stop }))
            .FirstOrDefault(item => item.Stop.DeliveryCalendarId == delivery.Id);

        if (match is null)
        {
            return null;
        }

        string? driverName = null;
        if (match.Route.DriverId.HasValue)
        {
            var driver = await driverRepository.GetByIdAsync(match.Route.DriverId.Value);
            if (driver is not null)
            {
                var driverUser = await userRepository.GetByIdAsync(driver.UserId);
                driverName = driverUser is null ? null : BuildUserFullName(driverUser);
            }
        }

        Vehicle? vehicle = null;
        if (match.Route.VehicleId.HasValue)
        {
            vehicle = await vehicleRepository.GetByIdAsync(match.Route.VehicleId.Value);
        }

        var issues = await deliveryIssueRepository.GetByRouteStopIdsAsync(new[] { match.Stop.Id });
        var latestIssue = issues
            .OrderByDescending(issue => issue.ReportedAt)
            .FirstOrDefault();

        return new TicketRouteContextDto
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

    private async Task<IReadOnlyList<TicketPackingBagContextDto>> BuildPackingBagsAsync(DeliveryCalendar delivery)
    {
        try
        {
            var board = await packingService.GetPackingBoardAsync(DateOnly.FromDateTime(delivery.DeliveryDate));
            return board.Routes
                .SelectMany(route => route.Bags)
                .Where(bag => bag.DeliveryCalendarId == delivery.Id)
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
                })
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<TicketPackingIncidentContextDto>> BuildPackingIncidentsAsync(DeliveryCalendar delivery)
    {
        var incidents = await packingIncidentService.SearchAsync(new PackingIncidentFilterDto
        {
            DeliveryCalendarId = delivery.Id,
        });

        return incidents
            .OrderByDescending(incident => incident.ReportedAt)
            .Select(incident => new TicketPackingIncidentContextDto
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
            })
            .ToArray();
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
        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);

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

    private async Task<List<TicketAttachmentDto>> MapTicketAttachmentsAsync(
        IEnumerable<TicketAttachment> attachments)
    {
        var list = mapper.Map<List<TicketAttachmentDto>>(attachments);
        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);

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

    private async Task<Order?> GetOrderWithItemsCachedAsync(
        int orderId,
        Dictionary<int, Order?> cache)
    {
        if (!cache.TryGetValue(orderId, out var order))
        {
            order = await orderRepository.GetWithItemsAndDeliveryAsync(orderId);
            cache[orderId] = order;
        }

        return order;
    }

    private async Task<Address?> GetAddressCachedAsync(
        int addressId,
        Dictionary<int, Address?> cache)
    {
        if (!cache.TryGetValue(addressId, out var address))
        {
            address = await addressRepository.GetByIdAsync(addressId);
            cache[addressId] = address;
        }

        return address;
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

    private static string BuildDietSummary(IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var labels = items
            .Select(item => $"{item.DietName} {item.VariantName}".Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

        var suffix = items.Count > labels.Length ? $" +{items.Count - labels.Length}" : string.Empty;
        return string.Join(", ", labels) + suffix;
    }

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
}
