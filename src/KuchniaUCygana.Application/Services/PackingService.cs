using AutoMapper;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

/// <summary>
/// Serwis aplikacyjny kompletacji — zarządza sesjami pakowania (torbami) i generowaniem etykiet.
/// </summary>
public sealed class PackingService : IPackingService
{
    private readonly IPackingSessionRepository _sessionRepository;
    private readonly IRepository<PackingItem> _itemRepository;
    private readonly IRepository<PackingLabel> _labelRepository;
    private readonly IOrderDataProvider _orderProvider;
    private readonly IDietDataProvider _dietProvider;
    private readonly IDeliveryManifestProvider _manifestProvider;
    private readonly IMapper _mapper;
    private readonly ILogger<PackingService> _logger;

    public PackingService(
        IPackingSessionRepository sessionRepository,
        IRepository<PackingItem> itemRepository,
        IRepository<PackingLabel> labelRepository,
        IOrderDataProvider orderProvider,
        IDietDataProvider dietProvider,
        IDeliveryManifestProvider manifestProvider,
        IMapper mapper,
        ILogger<PackingService> logger)
    {
        _sessionRepository = sessionRepository;
        _itemRepository = itemRepository;
        _labelRepository = labelRepository;
        _orderProvider = orderProvider;
        _dietProvider = dietProvider;
        _manifestProvider = manifestProvider;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy)
    {
        var existing = await _sessionRepository.GetActiveByDateAsync(date);
        var activeSession = existing.FirstOrDefault(s => s.PackedBy == packedBy && s.Status == PackingStatus.Pending);

        if (activeSession != null)
        {
            return _mapper.Map<PackingSessionDto>(activeSession);
        }

        var newSession = new PackingSession
        {
            PackingDate = date,
            PackedBy = packedBy,
            Status = PackingStatus.Pending,
        };

        var id = await _sessionRepository.InsertAsync(newSession);
        newSession.Id = id;

        _logger.LogInformation("Rozpoczęto nową sesję pakowania na dzień {Date} przez {User}", date, packedBy);
        return _mapper.Map<PackingSessionDto>(newSession);
    }

    /// <inheritdoc/>
    public async Task<PackingItemDto> PackClientDietAsync(int sessionId, int orderId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException($"Sesja pakowania {sessionId} nie istnieje.");

        var order = await _orderProvider.GetOrderByIdAsync(orderId)
            ?? throw new InvalidOperationException($"Zamówienie {orderId} nie istnieje.");

        // Aktualizuj sesję o dane klienta
        session.OrderId = orderId;
        session.ClientName = order.ClientName;
        await _sessionRepository.UpdateAsync(session);

        // Zazwyczaj pobralibyśmy dietę i utworzyli wiele pudełek. 
        // Implementacja skrócona na potrzeby demonstracji 1 zamówienia = 1 PackingItem.
        var packingItem = new PackingItem
        {
            PackingSessionId = sessionId,
            MealId = 0, // Będzie zależało od konkretnej logiki
            MealName = $"Zestaw dietetyczny wariant {order.DietVariantId}",
            DietVariantId = order.DietVariantId,
            IsDamaged = false,
        };

        var itemId = await _itemRepository.InsertAsync(packingItem);
        packingItem.Id = itemId;

        session.Status = PackingStatus.Packed;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Zapakowano zamówienie {OrderId} do sesji {SessionId}", orderId, sessionId);

        return _mapper.Map<PackingItemDto>(packingItem);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId)
    {
        var session = await _sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Sesja pakowania {sessionId} nie istnieje.");

        var labels = new List<PackingLabelDto>();

        // Etykieta wysyłkowa (na torbę)
        var shippingLabel = new PackingLabel
        {
            PackingSessionId = sessionId,
            LabelType = LabelType.Shipping,
            QrCode = Guid.NewGuid().ToString("N"),
            ClientName = session.ClientName,
            RouteInfo = session.RouteId.HasValue ? $"Trasa {session.RouteId}" : "Brak przypisanej trasy",
        };

        var shippingLabelId = await _labelRepository.InsertAsync(shippingLabel);
        shippingLabel.Id = shippingLabelId;
        labels.Add(_mapper.Map<PackingLabelDto>(shippingLabel));

        // Etykiety produktowe na każde pudełko
        foreach (var item in session.Items)
        {
            var productLabel = new PackingLabel
            {
                PackingItemId = item.Id,
                LabelType = LabelType.Product,
                QrCode = Guid.NewGuid().ToString("N"),
                DishName = item.MealName,
            };

            var labelId = await _labelRepository.InsertAsync(productLabel);
            productLabel.Id = labelId;
            labels.Add(_mapper.Map<PackingLabelDto>(productLabel));
        }

        session.Status = PackingStatus.Labeled;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Wygenerowano {Count} etykiet dla sesji {SessionId}", labels.Count, sessionId);

        return labels;
    }

    /// <inheritdoc/>
    public async Task ApproveDispatchAsync(int sessionId)
    {
        var session = await _sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Sesja pakowania {sessionId} nie istnieje.");

        session.Status = PackingStatus.Dispatched;
        await _sessionRepository.UpdateAsync(session);

        _logger.LogInformation("Sesja {SessionId} zatwierdzona do wysyłki.", sessionId);
    }
}
