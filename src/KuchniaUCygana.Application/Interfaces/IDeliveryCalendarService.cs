using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface IDeliveryCalendarService
{
    Task<IEnumerable<DeliveryCalendarDto>> GetByOrderIdAsync(int orderId);
    Task<bool> SkipDeliveryAsync(int deliveryCalendarId, int customerId, string reason);
    Task<bool> RescheduleDeliveryAsync(int deliveryCalendarId, DateTime newDate, int customerId);
    Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int month, int year);
    Task<bool> CanModifyDeliveryAsync(int deliveryCalendarId);
}
