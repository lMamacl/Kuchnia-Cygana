using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository orderRepository;
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;
    private readonly IAddressRepository addressRepository;
    private readonly IMapper mapper;
    private readonly IDietDataProvider dietDataProvider;
    private readonly IDietOrderingService dietOrderingService;

    public OrderService(
        IOrderRepository orderRepository,
        IDeliveryCalendarRepository deliveryCalendarRepository,
        IAddressRepository addressRepository,
        IMapper mapper,
        IDietDataProvider dietDataProvider,
        IDietOrderingService dietOrderingService)
    {
        this.orderRepository = orderRepository;
        this.deliveryCalendarRepository = deliveryCalendarRepository;
        this.addressRepository = addressRepository;
        this.mapper = mapper;
        this.dietDataProvider = dietDataProvider;
        this.dietOrderingService = dietOrderingService;
    }

    public async Task<IEnumerable<OrderSummaryDto>> GetByCustomerIdAsync(int customerId)
    {
        var orders = await orderRepository.GetByCustomerIdAsync(customerId);
        return mapper.Map<IEnumerable<OrderSummaryDto>>(orders);
    }

    public async Task<OrderHistoryPageDto> SearchByCustomerAsync(int customerId, OrderHistoryQueryDto query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 5, 50);
        var page = Math.Max(1, query.Page);

        var repositoryQuery = CreateCustomerOrderSearchQuery(customerId, query, page, pageSize);
        var (rows, totalCount) = await orderRepository.SearchByCustomerAsync(repositoryQuery);

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages)
        {
            page = totalPages;
            repositoryQuery = CreateCustomerOrderSearchQuery(customerId, query, page, pageSize);
            (rows, totalCount) = await orderRepository.SearchByCustomerAsync(repositoryQuery);
        }

        return new OrderHistoryPageDto
        {
            Items = rows.Select(MapOrderSummary).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<OrderDto?> GetByIdAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetWithItemsAndDeliveryAsync(orderId);
        if (order is null || order.CustomerId != customerId) return null;

        var orderDto = mapper.Map<OrderDto>(order);

        if (order.DeliveryDays.Count > 0)
        {
            var addressCache = new Dictionary<int, string>();
            foreach (var addrId in order.DeliveryDays.Select(d => d.AddressId).Distinct())
            {
                var address = await addressRepository.GetByIdAsync(addrId);
                addressCache[addrId] = address?.FullAddress ?? string.Empty;
            }

            foreach (var dayDto in orderDto.DeliveryDays)
            {
                if (addressCache.TryGetValue(dayDto.AddressId, out var fullAddr))
                    dayDto.AddressFullLine = fullAddr;
            }
        }

        return orderDto;
    }

    public async Task<int> CreateOrderAsync(CreateOrderRequest request, int customerId)
    {
        await ValidateAddressAsync(request.AddressId, customerId);

        var serverRequest = await RepriceRequestAsync(request);
        var materializedItems = await MaterializeOrderItemsAsync(serverRequest);
        var deliveryDates = materializedItems
            .Select(item => item.DeliveryDate?.Date)
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (deliveryDates.Count == 0)
            throw new InvalidOperationException("Nie udalo sie wyznaczyc dni dostawy dla zamowienia.");

        var totalPrice = materializedItems.Sum(i => i.TotalPrice);
        var now = DateTimeOffset.UtcNow;

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.PendingPayment,
            TotalPrice = totalPrice,
            DiscountAmount = 0m,
            FinalPrice = totalPrice,
            Notes = serverRequest.Notes,
            StartDate = deliveryDates.First(),
            EndDate = deliveryDates.Last(),
            CreatedAt = now,
        };

        foreach (var orderItem in materializedItems)
        {
            orderItem.CreatedAt = now;
        }

        var deliveryDays = BuildDeliveryCalendar(
            addressId: serverRequest.AddressId,
            deliveryWindowId: serverRequest.DeliveryWindowId,
            deliveryDates: deliveryDates);

        foreach (var day in deliveryDays)
        {
            day.CreatedAt = now;
        }

        return await orderRepository.InsertCheckoutAsync(order, materializedItems, deliveryDays);
    }

    public async Task<bool> CancelOrderAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId) return false;

        if (order.Status is not (OrderStatus.Draft or OrderStatus.PendingPayment))
            return false;

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        var deliveries = await deliveryCalendarRepository.GetByOrderIdAsync(orderId);
        foreach (var delivery in deliveries)
        {
            delivery.Status = DeliveryStatus.Cancelled;
            delivery.UpdatedAt = DateTimeOffset.UtcNow;
            await deliveryCalendarRepository.UpdateAsync(delivery);
        }

        return await orderRepository.UpdateAsync(order);
    }

    public async Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(int orderId, int customerId)
    {
        var orderDto = await GetByIdAsync(orderId, customerId)
            ?? throw new KeyNotFoundException($"Zamowienie {orderId} nie istnieje.");

        return new CheckoutSummaryDto
        {
            OrderId = orderId,
            Items = orderDto.Items,
            DeliveryDays = orderDto.DeliveryDays,
            TotalPrice = orderDto.TotalPrice,
            DiscountAmount = orderDto.DiscountAmount,
            FinalPrice = orderDto.FinalPrice,
        };
    }

    private async Task ValidateAddressAsync(int addressId, int customerId)
    {
        var address = await addressRepository.GetByIdAsync(addressId);
        if (address is null || address.UserId != customerId)
        {
            throw new InvalidOperationException("Wybrany adres dostawy nie istnieje albo nie nalezy do zalogowanego klienta.");
        }
    }

    private async Task<CreateOrderRequest> RepriceRequestAsync(CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Koszyk jest pusty.");

        var serverItems = new List<CreateOrderItemRequest>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var cartItem = await dietOrderingService.CreateCartItemAsync(item.DietVariantId, item.TotalDays);
            serverItems.Add(new CreateOrderItemRequest
            {
                DietId = cartItem.DietId,
                DietVariantId = cartItem.DietVariantId,
                DietName = cartItem.DietName,
                VariantName = cartItem.VariantName,
                CaloriesPerDay = cartItem.CaloriesPerDay,
                PricePerDay = cartItem.PricePerDay,
                TotalDays = cartItem.TotalDays,
            });
        }

        return new CreateOrderRequest
        {
            AddressId = request.AddressId,
            DeliveryWindowId = request.DeliveryWindowId,
            StartDate = request.StartDate,
            Notes = request.Notes,
            Items = serverItems,
        };
    }

    private async Task<List<OrderItem>> MaterializeOrderItemsAsync(CreateOrderRequest request)
    {
        var snapshots = new Dictionary<DateOnly, PublishedDietPlanSnapshotDto>();
        var result = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            foreach (var deliveryDateTime in BuildDeliveryDates(request.StartDate, item.TotalDays))
            {
                var deliveryDate = DateOnly.FromDateTime(deliveryDateTime);
                var snapshot = await GetPublishedSnapshotAsync(deliveryDate, snapshots);
                var planItems = snapshot.Items
                    .Where(planItem => planItem.DietVariantId == item.DietVariantId)
                    .OrderBy(planItem => planItem.SortOrder)
                    .ThenBy(planItem => planItem.DietMenuPlanItemId)
                    .ToList();

                if (planItems.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Opublikowany snapshot M2 na dzien {deliveryDate:yyyy-MM-dd} nie zawiera wariantu diety {item.DietVariantId}.");
                }

                AddMaterializedDayItems(result, item, deliveryDate, planItems);
            }
        }

        return result;
    }

    private async Task<PublishedDietPlanSnapshotDto> GetPublishedSnapshotAsync(
        DateOnly deliveryDate,
        Dictionary<DateOnly, PublishedDietPlanSnapshotDto> snapshots)
    {
        if (snapshots.TryGetValue(deliveryDate, out var cached))
        {
            return cached;
        }

        var snapshot = await dietDataProvider.GetPublishedPlanSnapshotAsync(deliveryDate);
        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Brak opublikowanego snapshotu M2 dla dnia {deliveryDate:yyyy-MM-dd}. Nie mozna utworzyc zamowienia bez faktycznego planu menu.");
        }

        snapshots[deliveryDate] = snapshot;
        return snapshot;
    }

    private static void AddMaterializedDayItems(
        List<OrderItem> result,
        CreateOrderItemRequest requestItem,
        DateOnly deliveryDate,
        IReadOnlyList<PublishedDietPlanItemDto> planItems)
    {
        var accumulatedPrice = 0m;
        for (var index = 0; index < planItems.Count; index++)
        {
            var planItem = planItems[index];
            var price = index == planItems.Count - 1
                ? requestItem.PricePerDay - accumulatedPrice
                : decimal.Round(requestItem.PricePerDay / planItems.Count, 2, MidpointRounding.AwayFromZero);
            accumulatedPrice += price;

            result.Add(new OrderItem
            {
                DietId = requestItem.DietId,
                DietVariantId = requestItem.DietVariantId,
                MealId = planItem.MealId,
                MealVariantId = planItem.MealVariantId,
                DietMenuPlanItemId = planItem.DietMenuPlanItemId,
                DietName = requestItem.DietName,
                VariantName = requestItem.VariantName,
                MealSlot = planItem.MealSlot,
                DeliveryDate = deliveryDate.ToDateTime(TimeOnly.MinValue),
                CaloriesPerDay = requestItem.CaloriesPerDay,
                PricePerDay = price,
                TotalDays = 1,
                TotalPrice = price,
            });
        }
    }

    private static IReadOnlyList<DateTime> BuildDeliveryDates(DateTime startDate, int totalDays)
    {
        if (totalDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(totalDays), "Liczba dni musi byc z zakresu 1-365.");

        var dates = new List<DateTime>(totalDays);
        var current = startDate.Date;

        while (dates.Count < totalDays)
        {
            if (current.DayOfWeek != DayOfWeek.Sunday)
            {
                dates.Add(current);
            }

            current = current.AddDays(1);
        }

        return dates;
    }

    private static List<DeliveryCalendar> BuildDeliveryCalendar(
        int addressId,
        int? deliveryWindowId,
        IReadOnlyList<DateTime> deliveryDates)
    {
        return deliveryDates
            .Select(date => new DeliveryCalendar
            {
                AddressId = addressId,
                DeliveryWindowId = deliveryWindowId,
                DeliveryDate = date.Date,
                Status = DeliveryStatus.Scheduled,
                IsSkipped = false,
                CutoffTime = new DateTimeOffset(
                    date.Date.AddDays(-1).AddHours(10),
                    TimeSpan.Zero),
            })
            .ToList();
    }

    private static CustomerOrderSearchQuery CreateCustomerOrderSearchQuery(
        int customerId,
        OrderHistoryQueryDto query,
        int page,
        int pageSize)
        => new()
        {
            CustomerId = customerId,
            Page = page,
            PageSize = pageSize,
            Status = query.Status,
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            OrderNumber = query.OrderNumber,
        };

    private static OrderSummaryDto MapOrderSummary(CustomerOrderSearchRow row)
        => new()
        {
            Id = row.Id,
            OrderNumber = row.OrderNumber,
            Status = row.Status,
            FinalPrice = row.FinalPrice,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            CreatedAt = row.CreatedAt,
            ItemCount = row.ItemCount,
        };
}
