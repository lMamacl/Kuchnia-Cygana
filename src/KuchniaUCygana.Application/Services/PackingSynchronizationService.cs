using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class PackingSynchronizationService : IPackingSynchronizationService
{
    private readonly IPackingSessionRepository sessionRepository;
    private readonly IPackingBagRepository bagRepository;
    private readonly IOrderDataProvider orderProvider;
    private readonly IRepository<PackingManifest>? manifestRepository;
    private readonly IDeliveryManifestProvider? manifestProvider;
    private readonly IPackingService? packingService;
    private readonly ILogger<PackingSynchronizationService> logger;

    public PackingSynchronizationService(
        IPackingSessionRepository sessionRepository,
        IPackingBagRepository bagRepository,
        IOrderDataProvider orderProvider,
        ILogger<PackingSynchronizationService> logger,
        IRepository<PackingManifest>? manifestRepository = null,
        IDeliveryManifestProvider? manifestProvider = null,
        IPackingService? packingService = null)
    {
        this.sessionRepository = sessionRepository;
        this.bagRepository = bagRepository;
        this.orderProvider = orderProvider;
        this.manifestRepository = manifestRepository;
        this.manifestProvider = manifestProvider;
        this.packingService = packingService;
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
            "Zsynchronizowano kompletację na {Date}: utworzono {CreatedSessions}, zaktualizowano {UpdatedSessions}, torby {CreatedBags}.",
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

    public async Task<PackingSynchronizationResultDto> RefreshFromRoutesAsync(DateOnly date, string requestedBy)
    {
        if (manifestProvider is null || packingService is null)
        {
            throw new InvalidOperationException("Synchronizacja kompletacji z trasami wymaga providerów M4 i serwisu kompletacji.");
        }

        var routes = (await manifestProvider.GetRoutesForDateAsync(date))
            .Where(route => route.RouteId > 0)
            .OrderBy(route => route.RouteId)
            .ToList();
        var routeStops = routes
            .SelectMany(route => route.Stops
                .Where(stop => stop.DeliveryCalendarId > 0)
                .OrderBy(stop => stop.SequenceNumber)
                .Select(stop => new RouteStopAssignment(route.RouteId, stop.DeliveryCalendarId)))
            .GroupBy(stop => stop.DeliveryCalendarId)
            .Select(group => group.First())
            .ToList();

        if (routeStops.Count == 0)
        {
            return new PackingSynchronizationResultDto
            {
                Date = date,
                RefreshedRoutes = routes.Count,
                TotalSessions = (await sessionRepository.GetByDateWithItemsAsync(date)).Count(),
            };
        }

        var activeOrders = (await orderProvider.GetActiveOrdersAsync(date))
            .GroupBy(o => o.DeliveryCalendarId > 0 ? o.DeliveryCalendarId : o.OrderId)
            .Select(g => g.First())
            .ToDictionary(o => o.DeliveryCalendarId > 0 ? o.DeliveryCalendarId : o.OrderId);

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
        var createdItems = 0;

        foreach (var routeStop in routeStops)
        {
            if (!activeOrders.TryGetValue(routeStop.DeliveryCalendarId, out var order))
            {
                if (!deliveries.TryGetValue(routeStop.DeliveryCalendarId, out var routeDelivery))
                {
                    continue;
                }

                order = new ActiveOrderEntry
                {
                    DeliveryCalendarId = routeDelivery.DeliveryCalendarId,
                    OrderId = routeDelivery.OrderId,
                    ClientId = routeDelivery.CustomerId,
                    ClientPublicId = routeDelivery.ClientPublicId,
                    ClientName = routeDelivery.CustomerFullName,
                    DietVariantId = routeDelivery.Items.FirstOrDefault()?.DietVariantId ?? 0,
                    DeliveryDate = DateOnly.FromDateTime(routeDelivery.DeliveryDate),
                };
            }

            deliveries.TryGetValue(routeStop.DeliveryCalendarId, out var delivery);
            var sessionKey = routeStop.DeliveryCalendarId;
            var clientPublicId = delivery?.ClientPublicId ?? order.ClientPublicId;
            if (string.IsNullOrWhiteSpace(clientPublicId))
            {
                clientPublicId = $"DC-{routeStop.DeliveryCalendarId}";
            }

            PackingSession session;
            if (existingSessions.TryGetValue(sessionKey, out var existing))
            {
                session = existing;
                var changed = false;
                if (session.DeliveryCalendarId != routeStop.DeliveryCalendarId)
                {
                    session.DeliveryCalendarId = routeStop.DeliveryCalendarId;
                    changed = true;
                }

                if (session.OrderId != order.OrderId)
                {
                    session.OrderId = order.OrderId;
                    changed = true;
                }

                if (!string.Equals(session.ClientName, order.ClientName, StringComparison.Ordinal))
                {
                    session.ClientName = order.ClientName;
                    changed = true;
                }

                if (!string.Equals(session.ClientPublicId, clientPublicId, StringComparison.Ordinal))
                {
                    session.ClientPublicId = clientPublicId;
                    changed = true;
                }

                if (changed)
                {
                    await sessionRepository.UpdateAsync(session);
                    updatedSessions++;
                }
            }
            else
            {
                session = new PackingSession
                {
                    PackingDate = date,
                    OrderId = order.OrderId,
                    ClientName = order.ClientName,
                    ClientPublicId = clientPublicId,
                    DeliveryCalendarId = routeStop.DeliveryCalendarId,
                    Status = PackingStatus.Pending,
                    PackedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy,
                };

                session.Id = await sessionRepository.InsertAsync(session);
                existingSessions[sessionKey] = session;
                createdSessions++;
            }

            createdBags += await EnsureDefaultBagAsync(session.Id);
            if (session.Items.Count == 0)
            {
                createdItems += (await packingService.PrepareOrderBoxesAsync(session.Id)).Count();
                var refreshed = await sessionRepository.GetWithItemsAsync(session.Id);
                if (refreshed is not null)
                {
                    existingSessions[sessionKey] = refreshed;
                }
            }
        }

        var supersededManifests = await MarkRouteManifestsForRegenerationAsync(
            date,
            routes.Select(route => route.RouteId));

        logger.LogInformation(
            "Odświeżono kompletację z M4 na {Date}: trasy {Routes}, sesje +{CreatedSessions}/{UpdatedSessions}, torby +{CreatedBags}, pudełka +{CreatedItems}.",
            date,
            routes.Count,
            createdSessions,
            updatedSessions,
            createdBags,
            createdItems);

        return new PackingSynchronizationResultDto
        {
            Date = date,
            CreatedSessions = createdSessions,
            UpdatedSessions = updatedSessions,
            CreatedBags = createdBags,
            CreatedItems = createdItems,
            RefreshedRoutes = routes.Count,
            SupersededManifests = supersededManifests,
            TotalSessions = existingSessions.Count,
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

    private async Task<int> MarkRouteManifestsForRegenerationAsync(DateOnly date, IEnumerable<int> routeIds)
    {
        if (manifestRepository is null)
        {
            return 0;
        }

        var ids = routeIds.Where(id => id > 0).Distinct().ToHashSet();
        if (ids.Count == 0)
        {
            return 0;
        }

        var manifests = (await manifestRepository.GetAllAsync())
            .Where(manifest => manifest.PackingDate == date &&
                manifest.RouteId.HasValue &&
                ids.Contains(manifest.RouteId.Value) &&
                !manifest.IsSuperseded)
            .ToList();

        foreach (var manifest in manifests)
        {
            manifest.RequiresRegeneration = true;
            manifest.RequiresRegenerationReason = "Kompletacja została odświeżona po zmianie danych logistycznych M4.";
            manifest.ChangeReason = "Odświeżenie kompletacji z logistyki.";
            await manifestRepository.UpdateAsync(manifest);
        }

        return manifests.Count;
    }

    private sealed record RouteStopAssignment(int RouteId, int DeliveryCalendarId);
}
