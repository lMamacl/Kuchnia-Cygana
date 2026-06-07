using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Domain.Interfaces.Orders;

public interface IDeliveryCalendarRepository : IRepository<DeliveryCalendar>
{
    Task<IEnumerable<DeliveryCalendar>> GetByOrderIdAsync(int orderId);
    Task<IEnumerable<DeliveryCalendar>> GetByDateRangeAsync(DateTime fromInclusive, DateTime toExclusive);
    // Pobiera wszystkie zaplanowane dostawy na dany dzień - używane przez M3.
    Task<IEnumerable<DeliveryCalendar>> GetScheduledForDateAsync(DateTime date);
    Task<bool> IsDateAvailableAsync(DateTime date);
}
