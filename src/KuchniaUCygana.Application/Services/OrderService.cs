using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository orderRepository;
    private readonly IOrderItemRepository orderItemRepository;
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;
    private readonly IAddressRepository addressRepository;
    private readonly IMapper mapper;

    public OrderService(
        IOrderRepository orderRepository,
        IOrderItemRepository orderItemRepository,
        IDeliveryCalendarRepository deliveryCalendarRepository,
        IAddressRepository addressRepository,
        IMapper mapper)
    {
        this.orderRepository = orderRepository;
        this.orderItemRepository = orderItemRepository;
        this.deliveryCalendarRepository = deliveryCalendarRepository;
        this.addressRepository = addressRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<OrderSummaryDto>> GetByCustomerIdAsync(int customerId)
    {
        var orders = await orderRepository.GetByCustomerIdAsync(customerId);
        return mapper.Map<IEnumerable<OrderSummaryDto>>(orders);
    }

    public async Task<OrderDto?> GetByIdAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetWithItemsAndDeliveryAsync(orderId);
        if (order is null || order.CustomerId != customerId) return null;

        var orderDto = mapper.Map<OrderDto>(order);

        // Uzupelniamy AddressFullLine dla dni dostawy (brak lazy loading w OrmLite)
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
        // 1. Generuj unikalny numer zamowienia
        var orderNumber = await orderRepository.GenerateOrderNumberAsync();

        // 2. Oblicz ceny
        var totalPrice = request.Items.Sum(i => i.PricePerDay * i.TotalDays);

        // 3. Utworz naglowek zamowienia
        var order = new Order
        {
            CustomerId = customerId,
            OrderNumber = orderNumber,
            Status = OrderStatus.PendingPayment,
            TotalPrice = totalPrice,
            DiscountAmount = 0m,
            FinalPrice = totalPrice,
            Notes = request.Notes,
            StartDate = request.StartDate,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var orderId = await orderRepository.InsertAsync(order);

        // 4. Zapisz pozycje zamowienia
        foreach (var item in request.Items)
        {
            var orderItem = new OrderItem
            {
                OrderId = orderId,
                DietId = item.DietId,
                DietVariantId = item.DietVariantId,
                DietName = item.DietName,
                VariantName = item.VariantName,
                CaloriesPerDay = item.CaloriesPerDay,
                PricePerDay = item.PricePerDay,
                TotalDays = item.TotalDays,
                TotalPrice = item.PricePerDay * item.TotalDays,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await orderItemRepository.InsertAsync(orderItem);
        }

        // 5. Generuj kalendarz dostaw
        var totalDays = request.Items.Max(i => i.TotalDays);
        var deliveryDays = BuildDeliveryCalendar(
            orderId: orderId,
            addressId: request.AddressId,
            deliveryWindowId: request.DeliveryWindowId,
            startDate: request.StartDate,
            totalDays: totalDays);

        foreach (var day in deliveryDays)
        {
            day.CreatedAt = DateTimeOffset.UtcNow;
            await deliveryCalendarRepository.InsertAsync(day);
        }

        // 6. Zaktualizuj EndDate w zamowieniu
        if (deliveryDays.Any())
        {
            order.Id = orderId;
            order.EndDate = deliveryDays.Last().DeliveryDate;
            await orderRepository.UpdateAsync(order);
        }

        return orderId;
    }

    public async Task<bool> CancelOrderAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId) return false;

        if (order.Status is not (OrderStatus.Draft or OrderStatus.PendingPayment))
            return false;

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTimeOffset.UtcNow;
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

    // Logika biznesowa generowania kalendarza dostaw
    private static List<DeliveryCalendar> BuildDeliveryCalendar(
        int orderId, int addressId, int? deliveryWindowId,
        DateTime startDate, int totalDays)
    {
        var calendar = new List<DeliveryCalendar>();
        var current = startDate.Date;
        var daysAdded = 0;

        while (daysAdded < totalDays)
        {
            calendar.Add(new DeliveryCalendar
            {
                OrderId = orderId,
                AddressId = addressId,
                DeliveryWindowId = deliveryWindowId,
                DeliveryDate = current,
                Status = DeliveryStatus.Scheduled,
                IsSkipped = false,
                CutoffTime = new DateTimeOffset(
                    current.AddDays(-1).Date.AddHours(10),
                    TimeSpan.Zero),
            });

            daysAdded++;
            current = current.AddDays(1);
        }

        return calendar;
    }
}
