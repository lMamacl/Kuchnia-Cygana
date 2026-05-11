using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto?> GetByIdAsync(int orderId, int customerId);
    Task<IEnumerable<OrderSummaryDto>> GetByCustomerIdAsync(int customerId);
    Task<int> CreateOrderAsync(CreateOrderRequest request, int customerId);
    Task<bool> CancelOrderAsync(int orderId, int customerId);
    Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(int orderId, int customerId);
}
