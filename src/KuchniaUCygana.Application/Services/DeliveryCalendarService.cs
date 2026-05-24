using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class DeliveryCalendarService : IDeliveryCalendarService
{
    private readonly IDeliveryCalendarRepository deliveryCalendarRepository;
    private readonly IOrderRepository orderRepository;
    private readonly IMapper mapper;

    public DeliveryCalendarService(
        IDeliveryCalendarRepository deliveryCalendarRepository,
        IOrderRepository orderRepository,
        IMapper mapper)
    {
        this.deliveryCalendarRepository = deliveryCalendarRepository;
        this.orderRepository = orderRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<DeliveryCalendarDto>> GetByOrderIdAsync(int orderId)
    {
        var items = await deliveryCalendarRepository.GetByOrderIdAsync(orderId);
        return mapper.Map<IEnumerable<DeliveryCalendarDto>>(items);
    }

    public async Task<DeliveryCalendarDto?> GetByIdAsync(int deliveryCalendarId)
    {
        var item = await deliveryCalendarRepository.GetByIdAsync(deliveryCalendarId);
        return item is null ? null : mapper.Map<DeliveryCalendarDto>(item);
    }

    public async Task<bool> CanModifyDeliveryAsync(int deliveryCalendarId)
    {
        var entry = await deliveryCalendarRepository.GetByIdAsync(deliveryCalendarId);
        if (entry is null || entry.IsDeleted) return false;

        // Zmiana możliwa tylko do wyznaczonego czasu (CutoffTime)
        return entry.CutoffTime.HasValue && DateTimeOffset.UtcNow < entry.CutoffTime.Value;
    }

    public async Task<bool> SkipDeliveryAsync(int deliveryCalendarId, int customerId, string reason)
    {
        var entry = await deliveryCalendarRepository.GetByIdAsync(deliveryCalendarId);
        if (entry is null) return false;

        var order = await orderRepository.GetByIdAsync(entry.OrderId);
        if (order is null || order.CustomerId != customerId) return false;

        if (!await CanModifyDeliveryAsync(deliveryCalendarId)) return false;

        entry.IsSkipped = true;
        entry.SkipReason = reason;
        entry.Status = DeliveryStatus.Skipped;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        return await deliveryCalendarRepository.UpdateAsync(entry);
    }

    public async Task<bool> RescheduleDeliveryAsync(int deliveryCalendarId, DateTime newDate, int? newAddressId, int customerId)
    {
        var entry = await deliveryCalendarRepository.GetByIdAsync(deliveryCalendarId);
        if (entry is null) return false;

        var order = await orderRepository.GetByIdAsync(entry.OrderId);
        if (order is null || order.CustomerId != customerId) return false;

        if (!await CanModifyDeliveryAsync(deliveryCalendarId)) return false;

        // Logika zmiany daty - zakładamy nowy CutoffTime o 10:00 dnia poprzedniego
        entry.DeliveryDate = newDate.Date;
        entry.CutoffTime = new DateTimeOffset(newDate.Date.AddDays(-1).AddHours(10), TimeSpan.Zero);

        if (newAddressId.HasValue)
        {
            entry.AddressId = newAddressId.Value;
        }

        // Jeśli wcześniej była pominięta, przywracamy
        entry.IsSkipped = false;
        entry.Status = DeliveryStatus.Scheduled;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        return await deliveryCalendarRepository.UpdateAsync(entry);
    }

    public Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int month, int year)
    {
        var dates = new List<DateTime>();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var tomorrow = DateTime.Today.AddDays(1);

        for (int i = 1; i <= daysInMonth; i++)
        {
            var d = new DateTime(year, month, i);
            if (d >= tomorrow)
            {
                dates.Add(d);
            }
        }
        return Task.FromResult<IEnumerable<DateTime>>(dates);
    }
}
