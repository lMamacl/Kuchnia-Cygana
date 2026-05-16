using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
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

        if (order is null || order.CustomerId != customerId)
            return null;

        var orderDto = mapper.Map<OrderDto>(order);

        // Uzupełnia AddressFullLine dla każdego dnia dostawy
        if (order.DeliveryDays.Count > 0)
        {
            var addressIds = order.DeliveryDays
                .Select(d => d.AddressId)
                .Distinct()
                .ToList();

            var addressCache = new Dictionary<int, string>();
            foreach (var addrId in addressIds)
            {
                var address = await addressRepository.GetByIdAsync(addrId);
                addressCache[addrId] = address?.FullAddress ?? string.Empty;
            }

            foreach (var dayDto in orderDto.DeliveryDays)
            {
                if (addressCache.TryGetValue(dayDto.AddressId, out var fullAddress))
                    dayDto.AddressFullLine = fullAddress;
            }
        }

        return orderDto;
    }

    public async Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(int orderId, int customerId)
    {
        var orderDto = await GetByIdAsync(orderId, customerId)
            ?? throw new KeyNotFoundException($"Zamówienie {orderId} nie istnieje.");

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

    public Task<int> CreateOrderAsync(CreateOrderRequest request, int customerId) =>
        throw new NotImplementedException();

    public Task<bool> CancelOrderAsync(int orderId, int customerId) =>
        throw new NotImplementedException();
}
