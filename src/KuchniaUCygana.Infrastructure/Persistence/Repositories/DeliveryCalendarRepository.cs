using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryCalendarRepository : BaseRepository<DeliveryCalendar>, IDeliveryCalendarRepository
{
    public DeliveryCalendarRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<DeliveryCalendar>> GetByOrderIdAsync(int orderId)
    {
        using var db = Factory.CreateConnection();
        var q = db.From<DeliveryCalendar>()
            .Where(x => x.OrderId == orderId && x.IsDeleted == false)
            .OrderBy(x => x.DeliveryDate);
        return await db.SelectAsync(q);
    }

    public async Task<IEnumerable<DeliveryCalendar>> GetScheduledForDateAsync(DateTime date)
    {
        using var db = Factory.CreateConnection();
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        return await db.SelectAsync<DeliveryCalendar>(x =>
            x.DeliveryDate >= dateOnly &&
            x.DeliveryDate < nextDay &&
            x.Status == DeliveryStatus.Scheduled &&
            x.IsSkipped == false &&
            x.IsDeleted == false);
    }

    public Task<bool> IsDateAvailableAsync(DateTime date)
    {
        return Task.FromResult(true);
    }
}
