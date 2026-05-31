using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class PackingSynchronizationService : IPackingSynchronizationService
{
    private readonly IPackingSessionRepository sessionRepository;
    private readonly IPackingBagRepository bagRepository;
    private readonly IOrderDataProvider orderProvider;
    private readonly ILogger<PackingSynchronizationService> logger;

    public PackingSynchronizationService(
        IPackingSessionRepository sessionRepository,
        IPackingBagRepository bagRepository,
        IOrderDataProvider orderProvider,
        ILogger<PackingSynchronizationService> logger)
    {
        this.sessionRepository = sessionRepository;
        this.bagRepository = bagRepository;
        this.orderProvider = orderProvider;
        this.logger = logger;
    }

    public async Task<PackingSynchronizationResultDto> EnsureSessionsForDateAsync(DateOnly date, string requestedBy)
    {
        var activeOrders = (await orderProvider.GetActiveOrdersAsync(date))
            .GroupBy(o => o.DeliveryCalendarId > 0 ? o.DeliveryCalendarId : o.OrderId)
            .Select(g => g.First())
            .OrderBy(o => o.DeliveryCalendarId)
            .ThenBy(o => o.OrderId)
            .ToList();

        var deliveries = (await orderProvider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .GroupBy(d => d.DeliveryCalendarId > 0 ? d.DeliveryCalendarId : d.OrderId)
            .ToDictionary(g => g.Key, g => g.First());

        var existingSessions = (await sessionRepository.GetByDateWithItemsAsync(date))
            .Where(s => s.DeliveryCalendarId.HasValue || s.OrderId.HasValue)
            .GroupBy(s => s.DeliveryCalendarId ?? s.OrderId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var createdSessions = 0;
        var updatedSessions = 0;
        var createdBags = 0;

        foreach (var order in activeOrders)
        {
            var deliveryCalendarId = order.DeliveryCalendarId > 0
                ? order.DeliveryCalendarId
                : 0;
            var sessionKey = deliveryCalendarId > 0 ? deliveryCalendarId : order.OrderId;
            deliveries.TryGetValue(sessionKey, out var delivery);

            if (deliveryCalendarId <= 0 && delivery?.DeliveryCalendarId > 0)
            {
                deliveryCalendarId = delivery.DeliveryCalendarId;
                sessionKey = deliveryCalendarId;
            }

            var clientPublicId = delivery?.ClientPublicId ?? order.ClientPublicId;
            if (string.IsNullOrWhiteSpace(clientPublicId))
            {
                clientPublicId = deliveryCalendarId > 0
                    ? $"DC-{deliveryCalendarId}"
                    : $"ORD-{order.OrderId}";
            }

            if (existingSessions.TryGetValue(sessionKey, out var existing))
            {
                var changed = false;
                if (!existing.DeliveryCalendarId.HasValue && deliveryCalendarId > 0)
                {
                    existing.DeliveryCalendarId = deliveryCalendarId;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.ClientName) && !string.IsNullOrWhiteSpace(order.ClientName))
                {
                    existing.ClientName = order.ClientName;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.ClientPublicId) && !string.IsNullOrWhiteSpace(clientPublicId))
                {
                    existing.ClientPublicId = clientPublicId;
                    changed = true;
                }

                if (changed)
                {
                    await sessionRepository.UpdateAsync(existing);
                    updatedSessions++;
                }

                createdBags += await EnsureDefaultBagAsync(existing.Id);
                continue;
            }

            var session = new PackingSession
            {
                PackingDate = date,
                OrderId = order.OrderId,
                ClientName = order.ClientName,
                ClientPublicId = clientPublicId,
                DeliveryCalendarId = deliveryCalendarId > 0 ? deliveryCalendarId : null,
                Status = PackingStatus.Pending,
                PackedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy,
            };

            session.Id = await sessionRepository.InsertAsync(session);
            existingSessions[sessionKey] = session;
            createdSessions++;
            createdBags += await EnsureDefaultBagAsync(session.Id);
        }

        var totalSessions = existingSessions.Count;
        logger.LogInformation(
            "Zsynchronizowano kompletacje na {Date}: utworzono {CreatedSessions}, zaktualizowano {UpdatedSessions}, torby {CreatedBags}.",
            date,
            createdSessions,
            updatedSessions,
            createdBags);

        return new PackingSynchronizationResultDto
        {
            Date = date,
            CreatedSessions = createdSessions,
            UpdatedSessions = updatedSessions,
            CreatedBags = createdBags,
            TotalSessions = totalSessions,
        };
    }

    private async Task<int> EnsureDefaultBagAsync(int sessionId)
    {
        var existing = await bagRepository.GetDefaultForSessionAsync(sessionId);
        if (existing is not null)
        {
            return 0;
        }

        var bag = new PackingBag
        {
            PackingSessionId = sessionId,
            BagNumber = 1,
            BagCode = $"BAG-{sessionId:D6}-01",
        };

        await bagRepository.InsertAsync(bag);
        return 1;
    }
}
