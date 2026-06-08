using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Domain.Interfaces.Orders;

public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId);
    Task<Order?> GetWithItemsAndDeliveryAsync(int orderId);
    Task<IReadOnlyDictionary<int, Order>> GetWithItemsAndDeliveryByIdsAsync(IEnumerable<int> orderIds);
    Task<IEnumerable<Order>> GetActiveOrdersForDateAsync(DateTime date);
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    Task<string> GenerateOrderNumberAsync();
}
