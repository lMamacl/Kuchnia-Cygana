using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Orders WHERE CustomerId = @CustomerId AND IsDeleted = 0 ORDER BY CreatedAt DESC";
        return await db.QueryAsync<Order>(sql, new { CustomerId = customerId });
    }

    public async Task<Order?> GetWithItemsAndDeliveryAsync(int orderId)
    {
        using var db = Factory.CreateConnection();

        const string sqlOrder = "SELECT * FROM Orders WHERE Id = @OrderId AND IsDeleted = 0";
        var order = await db.QuerySingleOrDefaultAsync<Order>(sqlOrder, new { OrderId = orderId });

        if (order is null) return null;

        const string sqlItems = "SELECT * FROM OrderItems WHERE OrderId = @OrderId AND IsDeleted = 0";
        order.Items = (await db.QueryAsync<OrderItem>(sqlItems, new { OrderId = orderId })).ToList();

        const string sqlDays = "SELECT * FROM DeliveryCalendar WHERE OrderId = @OrderId AND IsDeleted = 0 ORDER BY DeliveryDate";
        order.DeliveryDays = (await db.QueryAsync<DeliveryCalendar>(sqlDays, new { OrderId = orderId })).ToList();

        return order;
    }

    public async Task<IEnumerable<Order>> GetActiveOrdersForDateAsync(DateTime date)
    {
        using var db = Factory.CreateConnection();
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        const string sql = @"
            SELECT o.* FROM Orders o
            INNER JOIN DeliveryCalendar d ON o.Id = d.OrderId
            WHERE o.IsDeleted = 0
              AND d.DeliveryDate >= @DateOnly 
              AND d.DeliveryDate < @NextDay 
              AND d.IsDeleted = 0 
              AND d.IsSkipped = 0";

        return await db.QueryAsync<Order>(sql, new { DateOnly = dateOnly, NextDay = nextDay });
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Orders WHERE OrderNumber = @OrderNumber AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<Order>(sql, new { OrderNumber = orderNumber });
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        using var db = Factory.CreateConnection();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        const string sql = "SELECT COUNT(1) FROM Orders WHERE CreatedAt >= @Today AND CreatedAt < @Tomorrow";
        var count = await db.ExecuteScalarAsync<int>(sql, new { Today = today, Tomorrow = tomorrow });

        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"ORD-{dateStr}-{(count + 1):D4}";
    }
}
