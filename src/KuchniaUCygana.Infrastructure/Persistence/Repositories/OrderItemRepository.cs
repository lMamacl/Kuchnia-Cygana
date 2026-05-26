using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class OrderItemRepository : BaseRepository<OrderItem>, IOrderItemRepository
{
    public OrderItemRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM OrderItems WHERE OrderId = @OrderId AND IsDeleted = 0";
        return await db.QueryAsync<OrderItem>(sql, new { OrderId = orderId });
    }
}
