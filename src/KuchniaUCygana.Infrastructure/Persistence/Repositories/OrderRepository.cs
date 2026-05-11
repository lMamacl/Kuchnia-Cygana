using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId) =>
        throw new NotImplementedException();

    public Task<Order?> GetWithItemsAndDeliveryAsync(int orderId) =>
        throw new NotImplementedException();

    public Task<IEnumerable<Order>> GetActiveOrdersForDateAsync(DateTime date) =>
        throw new NotImplementedException();

    public Task<Order?> GetByOrderNumberAsync(string orderNumber) =>
        throw new NotImplementedException();

    public Task<string> GenerateOrderNumberAsync() =>
        throw new NotImplementedException();
}
