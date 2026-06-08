using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Orders;

public interface IDeliveryCalendarRepository : IRepository<DeliveryCalendar>
{
    Task<IEnumerable<DeliveryCalendar>> GetByOrderIdAsync(int orderId);
    Task<IReadOnlyList<DeliveryCalendar>> GetByIdsAsync(IEnumerable<int> ids);
    Task<IReadOnlyList<TicketDeliveryOptionLookupRow>> SearchTicketDeliveryOptionsAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        int limit);
    Task<IEnumerable<DeliveryCalendar>> GetByDateRangeAsync(DateTime fromInclusive, DateTime toExclusive);
    // Pobiera wszystkie zaplanowane dostawy na dany dzień - używane przez M3.
    Task<IEnumerable<DeliveryCalendar>> GetScheduledForDateAsync(DateTime date);
    Task<bool> IsDateAvailableAsync(DateTime date);
}

public sealed class TicketDeliveryOptionLookupRow
{
    public int DeliveryCalendarId { get; set; }

    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerFullName { get; set; } = string.Empty;

    public DateTime DeliveryDate { get; set; }

    public DeliveryStatus DeliveryStatus { get; set; }

    public string AddressFullLine { get; set; } = string.Empty;

    public string DietSummary { get; set; } = string.Empty;
}
