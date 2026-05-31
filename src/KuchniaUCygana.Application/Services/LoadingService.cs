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
    private readonly IPackingBagRepository _bagRepository;
    private readonly IRepository<PackingLabel> _labelRepository;
    private readonly IRepository<PackingManifest> _packingManifestRepository;
    private readonly IPackingStatusLogRepository _statusLogRepository;
    private readonly IPackingService _packingService;
    private readonly IMapper _mapper;
    private readonly ILogger<LoadingService> _logger;

    public LoadingService(
        IPackingSessionRepository sessionRepository,
        IPackingBagRepository bagRepository,
        IRepository<PackingLabel> labelRepository,
        IRepository<PackingManifest> packingManifestRepository,
        IPackingStatusLogRepository statusLogRepository,
        IPackingService packingService,
        IMapper mapper,
        ILogger<LoadingService> logger)
    {
        _sessionRepository = sessionRepository;
        _bagRepository = bagRepository;
        _labelRepository = labelRepository;
        _packingManifestRepository = packingManifestRepository;
        _statusLogRepository = statusLogRepository;
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
            throw new InvalidOperationException("Torbę można załadować dopiero po wygenerowaniu i weryfikacji manifestu dostawy.");
        }

        if (session.Status is not (PackingStatus.Labeled or PackingStatus.Loaded or PackingStatus.Dispatched))
        {
            throw new InvalidOperationException("Do auta można załadować tylko torbę z etykietą transportową.");
        }

        var physicalBag = await _bagRepository.GetDefaultForSessionAsync(session.Id)
            ?? throw new InvalidOperationException("Dostawa nie ma fizycznej torby transportowej.");
        var hasTransportLabel = (await _labelRepository.GetAllAsync()).Any(label =>
            label.LabelType == LabelType.Shipping &&
            (label.PackingBagId == physicalBag.Id || label.PackingSessionId == session.Id));
        if (!hasTransportLabel)
        {
            throw new InvalidOperationException("Torbę można załadować dopiero po wydruku etykiety transportowej.");
        }

        if (session.Status != PackingStatus.Dispatched)
        {
            var oldStatus = session.Status;
            session.Status = PackingStatus.Loaded;
            await _sessionRepository.UpdateAsync(session);
            await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Torba załadowana do auta.");
        }

        if (physicalBag.Status != PackingBagStatus.Dispatched)
        {
            physicalBag.Status = PackingBagStatus.Loaded;
            physicalBag.LoadedAt = DateTimeOffset.UtcNow;
            await _bagRepository.UpdateAsync(physicalBag);
        }
    }

    /// <inheritdoc/>
    public async Task<PackingManifestDto> GenerateManifestAsync(
        DateOnly date,
        int routeId,
        string generatedBy,
        string? changeReason = null)
    {
        var board = await _packingService.GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);

        if (route.RouteId <= 0)
        {
            throw new InvalidOperationException("Manifest wymaga trasy M4. Dostawy bez trasy można kompletować, ale nie można ich załadować ani wysłać.");
        }

        if (!route.AllBagsPacked)
        {
            throw new InvalidOperationException("Manifest dostawy można wygenerować dopiero po spakowaniu wszystkich toreb dla tej trasy.");
        }

        var shippingLabels = (await _labelRepository.GetAllAsync())
            .Where(label => label.LabelType == LabelType.Shipping)
            .ToList();
        var labelsBySession = new Dictionary<int, PackingLabelDto>();

        foreach (var bag in route.Bags)
        {
            var transportLabel = FindLatestShippingLabel(shippingLabels, bag);
            if (transportLabel is null)
            {
                throw new InvalidOperationException($"Manifest wymaga wydrukowanej etykiety transportowej dla torby {bag.BagCode}.");
            }

            labelsBySession[bag.PackingSessionId] = new PackingLabelDto
            {
                Id = transportLabel.Id,
                PackingSessionId = transportLabel.PackingSessionId,
                PackingBagId = transportLabel.PackingBagId,
                LabelType = transportLabel.LabelType.ToString(),
                QrCode = transportLabel.QrCode,
                PrintNumber = transportLabel.PrintNumber,
                PrintedAt = transportLabel.PrintedAt,
                PrintedBy = transportLabel.PrintedBy,
                ReprintReason = transportLabel.ReprintReason,
            };
        }

        board = await _packingService.GetPackingBoardAsync(date);
        route = GetRouteOrThrow(board, routeId);

        var existingManifest = await GetLatestPackingManifestEntityAsync(date, routeId);
        var version = existingManifest?.ManifestVersion + 1 ?? 1;
        if (existingManifest is not null && string.IsNullOrWhiteSpace(changeReason))
        {
            changeReason = "Regeneracja manifestu po zmianie danych trasy lub toreb.";
        }

        var generatedAt = DateTimeOffset.UtcNow;
        var manifestNumber = $"PM-{date:yyyyMMdd}-R{route.RouteId:D2}-V{version:D2}";
        var payloadJson = BuildManifestPayload(board, route, labelsBySession, manifestNumber, generatedAt, generatedBy, version, changeReason);

        if (existingManifest is not null)
        {
            existingManifest.IsSuperseded = true;
            await _packingManifestRepository.UpdateAsync(existingManifest);
        }

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
            ManifestVersion = version,
            SupersedesManifestId = existingManifest?.Id,
            ChangeReason = changeReason,
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
            throw new InvalidOperationException("Manifest można zweryfikować tylko wtedy, gdy wszystkie torby są spakowane.");
        }

        var labels = (await _labelRepository.GetAllAsync())
            .Where(label => label.LabelType == LabelType.Shipping)
            .ToList();
        ValidateManifestPayload(manifest, route, labels);

        manifest.IsVerified = true;
        manifest.VerifiedAt = DateTimeOffset.UtcNow;
        manifest.VerifiedBy = string.IsNullOrWhiteSpace(verifiedBy) ? "System" : verifiedBy;
        await _packingManifestRepository.UpdateAsync(manifest);

        foreach (var routeBag in route.Bags)
        {
            var physicalBag = await _bagRepository.GetDefaultForSessionAsync(routeBag.PackingSessionId);
            if (physicalBag is not null && physicalBag.Status == PackingBagStatus.Labeled)
            {
                physicalBag.Status = PackingBagStatus.Manifested;
                physicalBag.ManifestedAt = DateTimeOffset.UtcNow;
                await _bagRepository.UpdateAsync(physicalBag);
            }
        }

        return _mapper.Map<PackingManifestDto>(manifest);
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(DateOnly date, int routeId)
    {
        var manifest = await GetManifestAsync(date, routeId)
            ?? throw new InvalidOperationException("Brak manifestu dla tej dostawy.");

        if (!manifest.IsVerified)
        {
            throw new InvalidOperationException("Dostawę można wysłać dopiero po weryfikacji manifestu.");
        }

        var board = await _packingService.GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);

        if (!route.AllBagsLoaded)
        {
            throw new InvalidOperationException("Dostawę można wysłać dopiero po załadowaniu wszystkich toreb do auta.");
        }

        foreach (var bag in route.Bags.Where(b => b.Status != nameof(PackingStatus.Dispatched)))
        {
            var session = await _sessionRepository.GetWithItemsAsync(bag.PackingSessionId)
                ?? throw new InvalidOperationException($"Torba pakowania {bag.PackingSessionId} nie istnieje.");

            var oldStatus = session.Status;
            session.Status = PackingStatus.Dispatched;
            await _sessionRepository.UpdateAsync(session);
            await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Dostawa wysłana z magazynu.");

            var physicalBag = await _bagRepository.GetDefaultForSessionAsync(session.Id);
            if (physicalBag is not null)
            {
                physicalBag.Status = PackingBagStatus.Dispatched;
                physicalBag.DispatchedAt = DateTimeOffset.UtcNow;
                await _bagRepository.UpdateAsync(physicalBag);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<PackingBagDto> LoadBagByCodeAsync(int routeId, string transportCode)
    {
        var labels = await _labelRepository.GetAllAsync();
        var normalizedCode = TransportLabelCodeNormalizer.Normalize(transportCode);
        var shippingLabels = labels
            .Where(l => l.LabelType == LabelType.Shipping)
            .ToList();
        var shippingLabel = shippingLabels.FirstOrDefault(l =>
            string.Equals(l.QrCode, transportCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.QrCode, normalizedCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(TransportLabelCodeNormalizer.Normalize(l.QrCode), normalizedCode, StringComparison.OrdinalIgnoreCase));

        PackingBag? physicalBag = null;

        if (shippingLabel is null)
        {
            physicalBag = await _bagRepository.GetByCodeAsync(normalizedCode);
            if (physicalBag is not null)
            {
                shippingLabel = shippingLabels
                    .Where(l => l.PackingBagId == physicalBag.Id || l.PackingSessionId == physicalBag.PackingSessionId)
                    .OrderByDescending(l => l.PrintNumber)
                    .ThenByDescending(l => l.Id)
                    .FirstOrDefault();
            }
            else if (int.TryParse(normalizedCode, out var directSessionId))
            {
                shippingLabel = await _sessionRepository.GetShippingLabelAsync(directSessionId);
            }
            else if (normalizedCode.StartsWith("BAG-", StringComparison.OrdinalIgnoreCase))
            {
                var parts = normalizedCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out var parsedSessionId))
                {
                    shippingLabel = await _sessionRepository.GetShippingLabelAsync(parsedSessionId);
                }
            }
        }

        if (shippingLabel is null || (!shippingLabel.PackingSessionId.HasValue && !shippingLabel.PackingBagId.HasValue))
        {
            throw new InvalidOperationException($"Błąd: Nie znaleziono torby z etykietą transportową dla kodu: {transportCode}");
        }

        if (shippingLabel.PackingBagId.HasValue)
        {
            physicalBag = await _bagRepository.GetByIdAsync(shippingLabel.PackingBagId.Value);
        }

        var sessionId = physicalBag?.PackingSessionId ?? shippingLabel.PackingSessionId!.Value;
        var session = await _sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania #{sessionId} nie istnieje.");

        // Dynamiczne rozwiązanie routeId (session nie ma już RouteId — ustalenia M3↔M4)
        var resolvedRouteId = await ResolveRouteIdForSessionAsync(session);
        if (!resolvedRouteId.HasValue)
        {
            throw new InvalidOperationException($"Błąd: Torba #{sessionId} nie ma przypisanej trasy.");
        }

        if (resolvedRouteId.Value != routeId)
        {
            var board = await _packingService.GetPackingBoardAsync(session.PackingDate);
            var correctRoute = board.Routes.FirstOrDefault(r => r.RouteId == resolvedRouteId.Value);
            var correctRouteName = correctRoute?.RouteName ?? $"Trasa #{resolvedRouteId}";
            throw new InvalidOperationException($"Błąd: Torba {transportCode} należy do innej trasy: {correctRouteName}.");
        }

        var manifest = await GetManifestAsync(session.PackingDate, routeId);
        if (manifest is null || !manifest.IsVerified)
        {
            throw new InvalidOperationException("Torbę można załadować dopiero po wygenerowaniu i weryfikacji manifestu dostawy.");
        }

        if (session.Status is not (PackingStatus.Labeled or PackingStatus.Loaded or PackingStatus.Dispatched))
        {
            throw new InvalidOperationException("Błąd: Torba nie została jeszcze zamknięta i oznaczona etykietą transportową.");
        }

        if (session.Status != PackingStatus.Loaded && session.Status != PackingStatus.Dispatched)
        {
            var oldStatus = session.Status;
            session.Status = PackingStatus.Loaded;
            await _sessionRepository.UpdateAsync(session);
            await LogStatusChangeAsync(session.Id, oldStatus, session.Status, $"Zeskanowano etykietę transportową {transportCode}.");
        }

        physicalBag ??= await _bagRepository.GetDefaultForSessionAsync(session.Id);
        if (physicalBag is not null && physicalBag.Status != PackingBagStatus.Dispatched)
        {
            physicalBag.Status = PackingBagStatus.Loaded;
            physicalBag.LoadedAt = DateTimeOffset.UtcNow;
            await _bagRepository.UpdateAsync(physicalBag);
        }

        var updatedBoard = await _packingService.GetPackingBoardAsync(session.PackingDate);
        var updatedRoute = updatedBoard.Routes.First(r => r.RouteId == routeId);
        var bagDto = updatedRoute.Bags.FirstOrDefault(b => b.PackingSessionId == sessionId)
            ?? throw new InvalidOperationException($"Błąd przy aktualizacji danych torby #{sessionId}.");

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
            .Where(m => m.PackingDate == date && m.RouteId == routeId && !m.IsSuperseded)
            .OrderByDescending(m => m.GeneratedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();
    }

    private static PackingRouteDto GetRouteOrThrow(PackingBoardDto board, int routeId)
    {
        return board.Routes.FirstOrDefault(r => r.RouteId == routeId)
            ?? throw new InvalidOperationException($"Dostawa/trasa {routeId} nie istnieje dla dnia {board.PackingDate:dd.MM.yyyy}.");
    }

    private static PackingLabel? FindLatestShippingLabel(IEnumerable<PackingLabel> labels, PackingBagDto bag)
    {
        return labels
            .Where(label => label.LabelType == LabelType.Shipping)
            .Where(label => label.PackingBagId == bag.PackingBagId || label.PackingSessionId == bag.PackingSessionId)
            .OrderByDescending(label => label.PrintNumber)
            .ThenByDescending(label => label.Id)
            .FirstOrDefault();
    }

    private static void ValidateManifestPayload(
        PackingManifest manifest,
        PackingRouteDto route,
        IReadOnlyList<PackingLabel> shippingLabels)
    {
        using var document = JsonDocument.Parse(manifest.PayloadJson);
        var root = document.RootElement;
        var routeElement = root.GetProperty("route");
        var manifestRouteId = routeElement.GetProperty("RouteId").GetInt32();

        if (manifestRouteId != route.RouteId)
        {
            throw new InvalidOperationException("Manifest dotyczy innej trasy niż aktualna dostawa.");
        }

        var packages = routeElement.GetProperty("packages").EnumerateArray().ToList();
        if (packages.Count != route.TotalBags)
        {
            throw new InvalidOperationException("Manifest nie zgadza się z aktualną liczbą toreb w dostawie.");
        }

        var packagesBySessionId = packages
            .ToDictionary(p => p.GetProperty("packageId").GetInt32(), p => p);

        foreach (var bag in route.Bags)
        {
            if (!packagesBySessionId.TryGetValue(bag.PackingSessionId, out var package))
            {
                throw new InvalidOperationException($"Manifest nie zawiera torby #{bag.PackingSessionId}.");
            }

            if (!bag.HasLabels)
            {
                throw new InvalidOperationException($"Torba #{bag.PackingSessionId} nie ma etykiety transportowej.");
            }

            var currentLabel = FindLatestShippingLabel(shippingLabels, bag)
                ?? throw new InvalidOperationException($"Torba #{bag.PackingSessionId} nie ma etykiety transportowej.");

            if (package.TryGetProperty("transportCode", out var transportCodeElement) &&
                !string.Equals(transportCodeElement.GetString(), currentLabel.QrCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Etykieta transportowa torby #{bag.PackingSessionId} zmieniła się po wygenerowaniu manifestu. Wygeneruj nową wersję manifestu.");
            }

            if (package.TryGetProperty("transportPrintNumber", out var printNumberElement) &&
                printNumberElement.GetInt32() != currentLabel.PrintNumber)
            {
                throw new InvalidOperationException($"Numer wydruku etykiety torby #{bag.PackingSessionId} nie zgadza się z manifestem. Wygeneruj nową wersję manifestu.");
            }
        }
    }

    private static string BuildManifestPayload(
        PackingBoardDto board,
        PackingRouteDto route,
        IReadOnlyDictionary<int, PackingLabelDto> labelsBySession,
        string manifestNumber,
        DateTimeOffset generatedAt,
        string generatedBy,
        int manifestVersion,
        string? changeReason)
    {
        var payload = new
        {
            manifestNumber,
            manifestVersion,
            changeReason,
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
                    packingBagId = bag.PackingBagId,
                    bag.BagCode,
                    bag.BagNumber,
                    bag.DeliveryCalendarId,
                    transportCode = labelsBySession.TryGetValue(bag.PackingSessionId, out var label)
                        ? label.QrCode
                        : $"BAG-{bag.PackingSessionId:D6}",
                    transportLabelId = labelsBySession.TryGetValue(bag.PackingSessionId, out var transportLabel)
                        ? transportLabel.Id
                        : 0,
                    transportPrintNumber = labelsBySession.TryGetValue(bag.PackingSessionId, out var printLabel)
                        ? printLabel.PrintNumber
                        : 0,
                    transportPrintedAt = labelsBySession.TryGetValue(bag.PackingSessionId, out var printedLabel)
                        ? printedLabel.PrintedAt
                        : null,
                    bag.OrderId,
                    bag.ClientName,
                    bag.ClientPublicId,
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

    private async Task LogStatusChangeAsync(
        int packingSessionId,
        PackingStatus oldStatus,
        PackingStatus newStatus,
        string? notes = null)
    {
        if (oldStatus == newStatus)
        {
            return;
        }

        await _statusLogRepository.InsertAsync(new PackingStatusLog
        {
            PackingSessionId = packingSessionId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            Notes = notes,
        });
    }
}
