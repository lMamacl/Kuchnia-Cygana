using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class OrderItemRepository : BaseRepository<OrderItem>, IOrderItemRepository
{
    public OrderItemRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId) =>
        throw new NotImplementedException();
}
