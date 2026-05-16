using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    public OrderRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId)
    {
        using var db = Factory.CreateConnection();
        var q = db.From<Order>()
            .Where(x => x.CustomerId == customerId && x.IsDeleted == false)
            .OrderByDescending(x => x.CreatedAt);
        return await db.SelectAsync(q);
    }

    public async Task<Order?> GetWithItemsAndDeliveryAsync(int orderId)
    {
        using var db = Factory.CreateConnection();

        var order = await db.SingleAsync<Order>(x =>
            x.Id == orderId && x.IsDeleted == false);

        if (order is null) return null;

        var items = await db.SelectAsync<OrderItem>(x =>
            x.OrderId == orderId && x.IsDeleted == false);
        order.Items = items.ToList();

        var deliveryDays = await db.SelectAsync(
            db.From<DeliveryCalendar>()
                .Where(x => x.OrderId == orderId && x.IsDeleted == false)
                .OrderBy(x => x.DeliveryDate));
        order.DeliveryDays = deliveryDays.ToList();

        return order;
    }

    public async Task<IEnumerable<Order>> GetActiveOrdersForDateAsync(DateTime date)
    {
        using var db = Factory.CreateConnection();
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        // Zamówienia mające zaplanowaną dostawę na dany dzień
        var q = db.From<Order>()
            .Join<DeliveryCalendar>((o, d) => o.Id == d.OrderId)
            .Where<Order>(o => o.IsDeleted == false)
            .And<DeliveryCalendar>(d =>
                d.DeliveryDate >= dateOnly &&
                d.DeliveryDate < nextDay &&
                d.IsDeleted == false &&
                d.IsSkipped == false);

        return await db.SelectAsync(q);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleAsync<Order>(x =>
            x.OrderNumber == orderNumber && x.IsDeleted == false);
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        using var db = Factory.CreateConnection();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Zlicza zamówienia z dzisiaj (od północy do końca dnia)
        var count = await db.CountAsync<Order>(x =>
            x.CreatedAt >= today && x.CreatedAt < tomorrow);

        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"ORD-{dateStr}-{(count + 1):D4}";
    }
}
