using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryCalendarRepository : BaseRepository<DeliveryCalendar>, IDeliveryCalendarRepository
{
    public DeliveryCalendarRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IEnumerable<DeliveryCalendar>> GetByOrderIdAsync(int orderId) =>
        throw new NotImplementedException();

    public Task<IEnumerable<DeliveryCalendar>> GetScheduledForDateAsync(DateTime date) =>
        throw new NotImplementedException();

    public Task<bool> IsDateAvailableAsync(DateTime date) =>
        throw new NotImplementedException();
}
