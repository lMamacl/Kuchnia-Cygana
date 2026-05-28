using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class LoadingService : ILoadingService
{
    private readonly IPackingSessionRepository _sessionRepository;
    private readonly IRepository<PackingLabel> _labelRepository;
    private readonly IRepository<PackingManifest> _packingManifestRepository;
    private readonly IPackingService _packingService;
    private readonly IMapper _mapper;
    private readonly ILogger<LoadingService> _logger;

    public LoadingService(
        IPackingSessionRepository sessionRepository,
        IRepository<PackingLabel> labelRepository,
        IRepository<PackingManifest> packingManifestRepository,
        IPackingService packingService,
        IMapper mapper,
        ILogger<LoadingService> logger)
    {
        _sessionRepository = sessionRepository;
        _labelRepository = labelRepository;
        _packingManifestRepository = packingManifestRepository;
        _packingService = packingService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LoadOrderBagAsync(int packingSessionId)
    {
        var session = await _sessionRepository.GetWithItemsAsync(packingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {packingSessionId} nie istnieje.");

        // Dynamiczne rozwiązanie routeId (ustalenia M3↔M4 z 28.05 — session nie ma już RouteId)
        var resolvedRouteId = await ResolveRouteIdForSessionAsync(session);
        if (!resolvedRouteId.HasValue)
        {
            throw new InvalidOperationException("Torba nie ma przypisanej trasy dostawy.");
        }

        var manifest = await GetManifestAsync(session.PackingDate, resolvedRouteId.Value);
        if (manifest is null || !manifest.IsVerified)
        {
            throw new InvalidOperationException("Torbe mozna zaladowac dopiero po wygenerowaniu i weryfikacji manifestu dostawy.");
        }

        if (session.Status is not (PackingStatus.Labeled or PackingStatus.Loaded or PackingStatus.Dispatched))
        {
            throw new InvalidOperationException("Do auta mozna zaladowac tylko oetykietowana torbe.");
        }

        if (session.Status != PackingStatus.Dispatched)
        {
            session.Status = PackingStatus.Loaded;
            await _sessionRepository.UpdateAsync(session);
        }
    }

    /// <inheritdoc/>
    public async Task<PackingManifestDto> GenerateManifestAsync(DateOnly date, int routeId, string generatedBy)
    {
        var board = await _packingService.GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);

        if (!route.AllBagsPacked)
        {
            throw new InvalidOperationException("Manifest dostawy mozna wygenerowac dopiero po spakowaniu wszystkich toreb dla tej trasy.");
        }

        var labelsBySession = new Dictionary<int, PackingLabelDto>();

        foreach (var bag in route.Bags)
        {
            var labels = (await _packingService.GenerateTransportLabelsAsync(bag.PackingSessionId)).ToList();
            var transportLabel = labels.FirstOrDefault(l => l.LabelType == nameof(LabelType.Shipping));
            if (transportLabel is not null)
            {
                labelsBySession[bag.PackingSessionId] = transportLabel;
            }
        }

        board = await _packingService.GetPackingBoardAsync(date);
        route = GetRouteOrThrow(board, routeId);

        var generatedAt = DateTimeOffset.UtcNow;
        var manifestNumber = $"PM-{date:yyyyMMdd}-R{route.RouteId:D2}-{generatedAt:HHmmss}";
        var payloadJson = BuildManifestPayload(board, route, labelsBySession, manifestNumber, generatedAt, generatedBy);

        var manifest = new PackingManifest
        {
            PackingDate = date,
            ManifestNumber = manifestNumber,
            RouteId = route.RouteId,
            RouteName = route.RouteName,
            VehicleId = route.VehicleId,
            VehicleRegistration = route.VehicleRegistration,
            RouteCount = 1,
            BagCount = route.TotalBags,
            GeneratedAt = generatedAt,
            GeneratedBy = string.IsNullOrWhiteSpace(generatedBy) ? "System" : generatedBy,
            IsVerified = false,
            PayloadJson = payloadJson,
        };

        var id = await _packingManifestRepository.InsertAsync(manifest);
        manifest.Id = id;

        _logger.LogInformation(
            "Zapisano manifest {ManifestNumber} dla trasy {RouteId} z {BagCount} torbami.",
            manifest.ManifestNumber,
            route.RouteId,
            manifest.BagCount);

        return _mapper.Map<PackingManifestDto>(manifest);
    }

    /// <inheritdoc/>
    public async Task<PackingManifestDto?> GetManifestAsync(DateOnly date, int routeId)
    {
        var latest = await GetLatestPackingManifestEntityAsync(date, routeId);
        return latest is null ? null : _mapper.Map<PackingManifestDto>(latest);
    }

    /// <inheritdoc/>
    public async Task<PackingManifestDto> VerifyManifestAsync(DateOnly date, int routeId, string verifiedBy)
    {
        var manifest = await GetLatestPackingManifestEntityAsync(date, routeId)
            ?? throw new InvalidOperationException("Najpierw wygeneruj manifest dla tej dostawy.");

        var board = await _packingService.GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);

        if (!route.AllBagsPacked)
        {
            throw new InvalidOperationException("Manifest mozna zweryfikowac tylko wtedy, gdy wszystkie torby sa spakowane.");
        }

        ValidateManifestPayload(manifest, route);

        manifest.IsVerified = true;
        manifest.VerifiedAt = DateTimeOffset.UtcNow;
        manifest.VerifiedBy = string.IsNullOrWhiteSpace(verifiedBy) ? "System" : verifiedBy;
        await _packingManifestRepository.UpdateAsync(manifest);

        return _mapper.Map<PackingManifestDto>(manifest);
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(DateOnly date, int routeId)
    {
        var manifest = await GetManifestAsync(date, routeId)
            ?? throw new InvalidOperationException("Brak manifestu dla tej dostawy.");

        if (!manifest.IsVerified)
        {
            throw new InvalidOperationException("Dostawe mozna wyslac dopiero po weryfikacji manifestu.");
        }

        var board = await _packingService.GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);

        if (!route.AllBagsLoaded)
        {
            throw new InvalidOperationException("Dostawe mozna wyslac dopiero po zaladowaniu wszystkich toreb do auta.");
        }

        foreach (var bag in route.Bags.Where(b => b.Status != nameof(PackingStatus.Dispatched)))
        {
            var session = await _sessionRepository.GetWithItemsAsync(bag.PackingSessionId)
                ?? throw new InvalidOperationException($"Torba pakowania {bag.PackingSessionId} nie istnieje.");

            session.Status = PackingStatus.Dispatched;
            await _sessionRepository.UpdateAsync(session);
        }
    }

    /// <inheritdoc/>
    public async Task<PackingBagDto> LoadBagByCodeAsync(int routeId, string transportCode)
    {
        var labels = await _labelRepository.GetAllAsync();
        var shippingLabel = labels.FirstOrDefault(l =>
            l.LabelType == LabelType.Shipping &&
            string.Equals(l.QrCode, transportCode, StringComparison.OrdinalIgnoreCase));

        if (shippingLabel is null)
        {
            if (transportCode.StartsWith("BAG-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(transportCode.Substring(4), out var parsedSessionId))
            {
                shippingLabel = await _sessionRepository.GetShippingLabelAsync(parsedSessionId);
            }
            else if (int.TryParse(transportCode, out var directSessionId))
            {
                shippingLabel = await _sessionRepository.GetShippingLabelAsync(directSessionId);
            }
        }

        if (shippingLabel is null || !shippingLabel.PackingSessionId.HasValue)
        {
            throw new InvalidOperationException($"Blad: Nie znaleziono oetykietowanej torby dla kodu: {transportCode}");
        }

        var sessionId = shippingLabel.PackingSessionId.Value;
        var session = await _sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania #{sessionId} nie istnieje.");

        // Dynamiczne rozwiązanie routeId (session nie ma już RouteId — ustalenia M3↔M4)
        var resolvedRouteId = await ResolveRouteIdForSessionAsync(session);
        if (!resolvedRouteId.HasValue)
        {
            throw new InvalidOperationException($"Blad: Torba #{sessionId} nie ma przypisanej trasy.");
        }

        if (resolvedRouteId.Value != routeId)
        {
            var board = await _packingService.GetPackingBoardAsync(session.PackingDate);
            var correctRoute = board.Routes.FirstOrDefault(r => r.RouteId == resolvedRouteId.Value);
            var correctRouteName = correctRoute?.RouteName ?? $"Trasa #{resolvedRouteId}";
            throw new InvalidOperationException($"Blad: Torba {transportCode} nalezy do innej trasy: {correctRouteName}!");
        }

        var manifest = await GetManifestAsync(session.PackingDate, routeId);
        if (manifest is null || !manifest.IsVerified)
        {
            throw new InvalidOperationException("Torbe mozna zaladowac dopiero po wygenerowaniu i weryfikacji manifestu dostawy.");
        }

        if (session.Status is not (PackingStatus.Labeled or PackingStatus.Loaded or PackingStatus.Dispatched))
        {
            throw new InvalidOperationException("Blad: Torba nie zostala jeszcze spakowana i oetykietowana.");
        }

        if (session.Status != PackingStatus.Loaded && session.Status != PackingStatus.Dispatched)
        {
            session.Status = PackingStatus.Loaded;
            await _sessionRepository.UpdateAsync(session);
        }

        var updatedBoard = await _packingService.GetPackingBoardAsync(session.PackingDate);
        var updatedRoute = updatedBoard.Routes.First(r => r.RouteId == routeId);
        var bagDto = updatedRoute.Bags.FirstOrDefault(b => b.PackingSessionId == sessionId)
            ?? throw new InvalidOperationException($"Blad przy aktualizacji danych torby #{sessionId}.");

        return bagDto;
    }

    /// <inheritdoc/>
    public async Task<PackingRouteDto?> GetRouteDetailsAsync(DateOnly date, int routeId)
    {
        var board = await _packingService.GetPackingBoardAsync(date);
        return board.Routes.FirstOrDefault(r => r.RouteId == routeId);
    }

    private async Task<PackingManifest?> GetLatestPackingManifestEntityAsync(DateOnly date, int routeId)
    {
        var manifests = await _packingManifestRepository.GetAllAsync();
        return manifests
            .Where(m => m.PackingDate == date && m.RouteId == routeId)
            .OrderByDescending(m => m.GeneratedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();
    }

    private static PackingRouteDto GetRouteOrThrow(PackingBoardDto board, int routeId)
    {
        return board.Routes.FirstOrDefault(r => r.RouteId == routeId)
            ?? throw new InvalidOperationException($"Dostawa/trasa {routeId} nie istnieje dla dnia {board.PackingDate:dd.MM.yyyy}.");
    }

    private static void ValidateManifestPayload(PackingManifest manifest, PackingRouteDto route)
    {
        using var document = JsonDocument.Parse(manifest.PayloadJson);
        var root = document.RootElement;
        var routeElement = root.GetProperty("route");
        var manifestRouteId = routeElement.GetProperty("RouteId").GetInt32();

        if (manifestRouteId != route.RouteId)
        {
            throw new InvalidOperationException("Manifest dotyczy innej trasy niz aktualna dostawa.");
        }

        var packages = routeElement.GetProperty("packages").EnumerateArray().ToList();
        if (packages.Count != route.TotalBags)
        {
            throw new InvalidOperationException("Manifest nie zgadza sie z aktualna liczba toreb w dostawie.");
        }

        var packageIds = packages
            .Select(p => p.GetProperty("packageId").GetInt32())
            .ToHashSet();

        foreach (var bag in route.Bags)
        {
            if (!packageIds.Contains(bag.PackingSessionId))
            {
                throw new InvalidOperationException($"Manifest nie zawiera torby #{bag.PackingSessionId}.");
            }

            if (!bag.HasLabels)
            {
                throw new InvalidOperationException($"Torba #{bag.PackingSessionId} nie ma etykiety transportowej.");
            }
        }
    }

    private static string BuildManifestPayload(
        PackingBoardDto board,
        PackingRouteDto route,
        IReadOnlyDictionary<int, PackingLabelDto> labelsBySession,
        string manifestNumber,
        DateTimeOffset generatedAt,
        string generatedBy)
    {
        var payload = new
        {
            manifestNumber,
            packingDate = board.PackingDate.ToString("yyyy-MM-dd"),
            generatedAt,
            generatedBy = string.IsNullOrWhiteSpace(generatedBy) ? "System" : generatedBy,
            totals = new
            {
                totalBags = route.TotalBags,
                packedBags = route.PackedBags,
                loadedBags = route.LoadedBags,
            },
            route = new
            {
                route.RouteId,
                route.RouteName,
                route.VehicleId,
                route.VehicleRegistration,
                route.TotalBags,
                route.PackedBags,
                route.LoadedBags,
                packages = route.Bags.Select(bag => new
                {
                    packageId = bag.PackingSessionId,
                    transportCode = labelsBySession.TryGetValue(bag.PackingSessionId, out var label)
                        ? label.QrCode
                        : $"BAG-{bag.PackingSessionId:D6}",
                    bag.OrderId,
                    bag.ClientName,
                    bag.Address,
                    bag.RouteId,
                    bag.RouteName,
                    bag.VehicleRegistration,
                    bag.StopNumber,
                    bag.DeliveryWindow,
                    bag.Status,
                    bag.StatusText,
                    boxes = new
                    {
                        total = bag.TotalBoxes,
                        packed = bag.PackedBoxes,
                    },
                }),
            },
        };

        return JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });
    }

    /// <summary>
    /// Dynamicznie rozwiązuje RouteId dla sesji pakowania.
    /// Po usunięciu RouteId z PackingSession (ustalenia M3↔M4 z 28.05),
    /// trasa jest pobierana z PackingBoardDto — torba jest wyszukiwana
    /// po PackingSessionId w kontekście tras danego dnia.
    /// TODO [Sprint 7.1.6]: Rozważyć dedykowaną metodę repo z JOIN-em
    /// zamiast ładowania całego boardu.
    /// </summary>
    private async Task<int?> ResolveRouteIdForSessionAsync(PackingSession session)
    {
        if (!session.DeliveryCalendarId.HasValue)
            return null;

        var board = await _packingService.GetPackingBoardAsync(session.PackingDate);
        var route = board.Routes.FirstOrDefault(r =>
            r.Bags.Any(b => b.PackingSessionId == session.Id));

        return route?.RouteId;
    }
}
