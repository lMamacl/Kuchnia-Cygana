using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class DeliveryCalendarService : IDeliveryCalendarService
{
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;

    public DeliveryCalendarService(IDeliveryCalendarRepository deliveryCalendarRepository)
    {
        this.deliveryCalendarRepository = deliveryCalendarRepository;
    }

    public Task<IEnumerable<DeliveryCalendarDto>> GetByOrderIdAsync(int orderId) =>
        throw new NotImplementedException();

    public Task<bool> SkipDeliveryAsync(int deliveryCalendarId, int customerId, string reason) =>
        throw new NotImplementedException();

    public Task<bool> RescheduleDeliveryAsync(int deliveryCalendarId, DateTime newDate, int customerId) =>
        throw new NotImplementedException();

    public Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int month, int year) =>
        throw new NotImplementedException();

    public Task<bool> CanModifyDeliveryAsync(int deliveryCalendarId) =>
        throw new NotImplementedException();
}
