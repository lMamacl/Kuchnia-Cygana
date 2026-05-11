using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository orderRepository;
    private readonly IOrderItemRepository orderItemRepository;
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;

    public OrderService(
        IOrderRepository orderRepository,
        IOrderItemRepository orderItemRepository,
        IDeliveryCalendarRepository deliveryCalendarRepository)
    {
        this.orderRepository = orderRepository;
        this.orderItemRepository = orderItemRepository;
        this.deliveryCalendarRepository = deliveryCalendarRepository;
    }

    public Task<OrderDto?> GetByIdAsync(int orderId, int customerId) =>
        throw new NotImplementedException();

    public Task<IEnumerable<OrderSummaryDto>> GetByCustomerIdAsync(int customerId) =>
        throw new NotImplementedException();

    public Task<int> CreateOrderAsync(CreateOrderRequest request, int customerId) =>
        throw new NotImplementedException();

    public Task<bool> CancelOrderAsync(int orderId, int customerId) =>
        throw new NotImplementedException();

    public Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(int orderId, int customerId) =>
        throw new NotImplementedException();
}
