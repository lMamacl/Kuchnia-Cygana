using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class CheckoutService : ICheckoutService
{
    private readonly IOrderRepository orderRepository;
    private readonly IPaymentRepository paymentRepository;
    private readonly IPaymentService paymentService;
    private readonly IAddressRepository addressRepository;
    private readonly IMapper mapper;

    public CheckoutService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IPaymentService paymentService,
        IAddressRepository addressRepository,
        IMapper mapper)
    {
        this.orderRepository = orderRepository;
        this.paymentRepository = paymentRepository;
        this.paymentService = paymentService;
        this.addressRepository = addressRepository;
        this.mapper = mapper;
    }

    public async Task<CheckoutSummaryDto> BuildSummaryAsync(int orderId, int customerId)
    {
        var order = await orderRepository.GetWithItemsAndDeliveryAsync(orderId);
        if (order is null || order.CustomerId != customerId)
            throw new KeyNotFoundException($"Zamowienie {orderId} nie istnieje.");

        var orderDto = mapper.Map<OrderDto>(order);

        // Uzupelnij adresy w dniach dostawy
        AddressDto? primaryAddress = null;
        if (order.DeliveryDays.Any())
        {
            var firstAddressId = order.DeliveryDays.First().AddressId;
            var addr = await addressRepository.GetByIdAsync(firstAddressId);
            if (addr is not null)
                primaryAddress = mapper.Map<AddressDto>(addr);

            var addressCache = new Dictionary<int, string>();
            foreach (var addrId in order.DeliveryDays.Select(d => d.AddressId).Distinct())
            {
                var a = await addressRepository.GetByIdAsync(addrId);
                addressCache[addrId] = a?.FullAddress ?? string.Empty;
            }
            foreach (var day in orderDto.DeliveryDays)
            {
                if (addressCache.TryGetValue(day.AddressId, out var fullAddr))
                    day.AddressFullLine = fullAddr;
            }
        }

        return new CheckoutSummaryDto
        {
            OrderId = orderId,
            Items = orderDto.Items,
            DeliveryDays = orderDto.DeliveryDays,
            SelectedAddress = primaryAddress,
            TotalPrice = order.TotalPrice,
            DiscountAmount = order.DiscountAmount,
            FinalPrice = order.FinalPrice,
        };
    }

    public Task<string> InitiatePaymentAsync(int orderId, int customerId) =>
        throw new NotImplementedException();

    public Task<bool> ConfirmPaymentAsync(string stripePaymentIntentId) =>
        throw new NotImplementedException();

    public Task<bool> HandlePaymentFailureAsync(string stripePaymentIntentId) =>
        throw new NotImplementedException();

    public Task<bool> CancelOrderOnMaxAttemptsAsync(int orderId) =>
        throw new NotImplementedException();
}
