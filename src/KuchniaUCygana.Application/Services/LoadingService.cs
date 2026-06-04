using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Constants;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class LoadingService : ILoadingService, IManifestService
{
    private readonly IPackingSessionRepository _sessionRepository;
    private readonly IPackingBagRepository _bagRepository;
    private readonly IRepository<PackingLabel> _labelRepository;
    private readonly IRepository<PackingManifest> _packingManifestRepository;
    private readonly IPackingStatusLogRepository _statusLogRepository;
    private readonly IPackingService _packingService;
    private readonly IMapper _mapper;
    private readonly ILogger<LoadingService> _logger;
    private readonly INotificationService? _notificationService;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IPackingIncidentRepository? _incidentRepository;
    private readonly IPackingLabelRepository? _packingLabelRepository;
    private readonly IPackingManifestRepository? _packingManifestQueryRepository;
    private readonly IPackingManifestIssueRepository? _manifestIssueRepository;

    public LoadingService(
        IPackingSessionRepository sessionRepository,
        IPackingBagRepository bagRepository,
        IRepository<PackingLabel> labelRepository,
        IRepository<PackingManifest> packingManifestRepository,
        IPackingStatusLogRepository statusLogRepository,
        IPackingService packingService,
        IMapper mapper,
        ILogger<LoadingService> logger,
        INotificationService? notificationService = null,
        ICurrentUserService? currentUserService = null,
        IPackingIncidentRepository? incidentRepository = null,
        IPackingLabelRepository? packingLabelRepository = null,
        IPackingManifestRepository? packingManifestQueryRepository = null,
        IPackingManifestIssueRepository? manifestIssueRepository = null)
    {
        _sessionRepository = sessionRepository;
        _bagRepository = bagRepository;
        _labelRepository = labelRepository;
        _packingManifestRepository = packingManifestRepository;
        _statusLogRepository = statusLogRepository;
        _packingService = packingService;
        _mapper = mapper;
        _logger = logger;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _incidentRepository = incidentRepository;
        _packingLabelRepository = packingLabelRepository;
        _packingManifestQueryRepository = packingManifestQueryRepository;
        _manifestIssueRepository = manifestIssueRepository;
    }

    public async Task<ManifestControlDto> GetManifestControlAsync(DateOnly date, int routeId)
    {
        var board = await _packingService.GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);
        var manifest = await GetLatestPackingManifestEntityAsync(date, routeId);
        var labels = await GetShippingLabelsForRouteAsync(route);
        var currentSnapshotHash = BuildManifestSnapshotHash(route, labels);

        if (manifest is not null)
        {
            var requiresRegeneration = string.IsNullOrWhiteSpace(manifest.SnapshotHash) ||
                !string.Equals(manifest.SnapshotHash, currentSnapshotHash, StringComparison.Ordinal);
            if (requiresRegeneration != manifest.RequiresRegeneration)
            {
                manifest.RequiresRegeneration = requiresRegeneration;
                manifest.RequiresRegenerationReason = requiresRegeneration
                    ? "Aktualny układ trasy, toreb albo etykiet różni się od snapshotu manifestu."
                    : null;
                await _packingManifestRepository.UpdateAsync(manifest);
            }
        }

        var dto = manifest is null ? null : _mapper.Map<PackingManifestDto>(manifest);
        var checklist = BuildManifestChecklist(route, dto);
        var issues = await BuildManifestIssuesAsync(date, route, dto, checklist);
        var hasOpenPackingIncident = issues.Any(issue =>
            issue.IssueType == "packing-incident" &&
            issue.IsBlocking &&
            !issue.IsResolved);

        return new ManifestControlDto
        {
            Date = date,
            Route = route,
            Manifest = dto,
            Checklist = checklist,
            Issues = issues,
            CanGenerateManifest = route.CanGenerateManifest && !hasOpenPackingIncident,
            CanWorkerApprove = dto is not null &&
                !dto.IsVerified &&
                !dto.WorkerApprovedAt.HasValue &&
                !dto.RequiresRegeneration &&
                !checklist.Any(item => item.IsBlocking && !item.IsComplete) &&
                !issues.Any(issue => issue.IsBlocking && !issue.IsResolved),
            CanSupervisorApprove = dto?.WorkerApprovedAt.HasValue == true &&
                !dto.IsVerified &&
                !dto.RequiresRegeneration &&
                route.AllBagsLoaded &&
                !checklist.Any(item => item.IsBlocking && !item.IsComplete) &&
                !issues.Any(issue => issue.IsBlocking && !issue.IsResolved),
        };
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
        EnsureManifestAllowsLoading(manifest);
        if (manifest is null)
        {
            throw new InvalidOperationException("Torbę można załadować dopiero po wygenerowaniu i weryfikacji manifestu dostawy.");
        }

        if (session.Status is not (PackingStatus.Labeled or PackingStatus.Loaded or PackingStatus.Dispatched))
        {
            throw new InvalidOperationException("Do auta można załadować tylko torbę z etykietą transportową.");
        }

        var physicalBag = await _bagRepository.GetDefaultForSessionAsync(session.Id)
            ?? throw new InvalidOperationException("Dostawa nie ma fizycznej torby transportowej.");
        var latestTransportLabel = await GetLatestShippingLabelForBagAsync(session.Id, physicalBag.Id);
        if (latestTransportLabel is null)
        {
            throw new InvalidOperationException("Torbę można załadować dopiero po wydruku etykiety transportowej.");
        }

        if (!latestTransportLabel.AttachedAt.HasValue)
        {
            throw new InvalidOperationException("Torbe mozna zaladowac dopiero po potwierdzeniu przyklejenia etykiety transportowej.");
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

        var shippingLabels = await GetShippingLabelsForRouteAsync(route);
        var labelsBySession = new Dictionary<int, PackingLabelDto>();

        foreach (var bag in route.Bags)
        {
            var transportLabel = FindLatestShippingLabel(shippingLabels, bag);
            if (transportLabel is null)
            {
                throw new InvalidOperationException($"Manifest wymaga wydrukowanej etykiety transportowej dla torby {bag.BagCode}.");
            }

            if (!transportLabel.AttachedAt.HasValue)
            {
                throw new InvalidOperationException($"Manifest wymaga potwierdzenia przyklejenia etykiety transportowej dla torby {bag.BagCode}.");
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
                IsAttached = transportLabel.AttachedAt.HasValue,
                AttachedAt = transportLabel.AttachedAt,
                AttachedBy = transportLabel.AttachedBy,
            };
        }

        await EnsureRouteHasNoOpenPackingIncidentsAsync(date, route);

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
        var snapshotHash = BuildManifestSnapshotHash(route, shippingLabels);

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
            WorkerApprovedAt = null,
            WorkerApprovedBy = null,
            WorkerApprovedByUserId = null,
            SentToLogisticsAt = null,
            SentToLogisticsByUserId = null,
            RequiresRegeneration = false,
            RequiresRegenerationReason = null,
            SnapshotHash = snapshotHash,
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
        var control = await GetManifestControlAsync(date, routeId);
        return control.Manifest;
    }

    public async Task<PackingManifestDto> ApproveManifestByWorkerAsync(DateOnly date, int routeId, string approvedBy)
    {
        var manifest = await GetLatestPackingManifestEntityAsync(date, routeId)
            ?? throw new InvalidOperationException("Najpierw wygeneruj manifest dla tej dostawy.");
        var control = await GetManifestControlAsync(date, routeId);
        EnsureManifestCanBeApproved(control, requireWorkerApproval: false);

        manifest.WorkerApprovedAt = DateTimeOffset.UtcNow;
        manifest.WorkerApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "System" : approvedBy;
        manifest.WorkerApprovedByUserId = _currentUserService?.GetUserId();
        await _packingManifestRepository.UpdateAsync(manifest);

        if (_notificationService is not null)
        {
            await _notificationService.CreateForRolesAsync(
                new Notification
                {
                    Type = "PackingManifest",
                    Severity = NotificationSeverity.Warning,
                    Title = "Manifest czeka na zatwierdzenie przełożonego",
                    Message = $"Manifest {manifest.ManifestNumber} dla trasy {control.Route.RouteName} został zatwierdzony przez pracownika.",
                    LinkUrl = $"/loading/{routeId}/manifest?date={date:yyyy-MM-dd}",
                    SourceType = nameof(PackingManifest),
                    SourceId = manifest.Id,
                },
                new[] { AppRoles.PackingManager, AppRoles.Admin });
        }

        return _mapper.Map<PackingManifestDto>(manifest);
    }

    public async Task<PackingManifestDto> ApproveManifestBySupervisorAsync(DateOnly date, int routeId, string approvedBy)
    {
        var manifest = await GetLatestPackingManifestEntityAsync(date, routeId)
            ?? throw new InvalidOperationException("Najpierw wygeneruj manifest dla tej dostawy.");
        var control = await GetManifestControlAsync(date, routeId);
        EnsureManifestCanBeApproved(control, requireWorkerApproval: true);

        manifest.IsVerified = true;
        manifest.VerifiedAt = DateTimeOffset.UtcNow;
        manifest.VerifiedBy = string.IsNullOrWhiteSpace(approvedBy) ? "System" : approvedBy;
        manifest.VerifiedByUserId = _currentUserService?.GetUserId();
        manifest.SentToLogisticsAt = DateTimeOffset.UtcNow;
        manifest.SentToLogisticsByUserId = _currentUserService?.GetUserId();
        await _packingManifestRepository.UpdateAsync(manifest);

        foreach (var routeBag in control.Route.Bags)
        {
            var physicalBag = await _bagRepository.GetByIdAsync(routeBag.PackingBagId);
            if (physicalBag is not null && physicalBag.Status == PackingBagStatus.Labeled)
            {
                physicalBag.Status = PackingBagStatus.Manifested;
                physicalBag.ManifestedAt = DateTimeOffset.UtcNow;
                await _bagRepository.UpdateAsync(physicalBag);
            }
        }

        if (_notificationService is not null)
        {
            await _notificationService.CreateForRolesAsync(
                new Notification
                {
                    Type = "PackingManifest",
                    Severity = NotificationSeverity.Success,
                    Title = "Manifest wysłany do logistyki",
                    Message = $"Manifest {manifest.ManifestNumber} dla trasy {control.Route.RouteName} został finalnie zatwierdzony.",
                    LinkUrl = $"/loading/{routeId}/manifest?date={date:yyyy-MM-dd}",
                    SourceType = nameof(PackingManifest),
                    SourceId = manifest.Id,
                },
                new[] { AppRoles.Logistics, AppRoles.LogisticsManager, AppRoles.Admin });
        }

        return _mapper.Map<PackingManifestDto>(manifest);
    }

    /// <inheritdoc/>
    public async Task<PackingManifestDto> VerifyManifestAsync(DateOnly date, int routeId, string verifiedBy)
    {
        return await ApproveManifestBySupervisorAsync(date, routeId, verifiedBy);
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

        foreach (var bag in route.Bags.Where(b => b.Status != nameof(PackingBagStatus.Dispatched)))
        {
            var session = await _sessionRepository.GetWithItemsAsync(bag.PackingSessionId)
                ?? throw new InvalidOperationException($"Torba pakowania {bag.PackingSessionId} nie istnieje.");

            var oldStatus = session.Status;
            session.Status = PackingStatus.Dispatched;
            await _sessionRepository.UpdateAsync(session);
            await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Dostawa wysłana z magazynu.");

            var physicalBag = await _bagRepository.GetByIdAsync(bag.PackingBagId);
            if (physicalBag is not null)
            {
                physicalBag.Status = PackingBagStatus.Dispatched;
                physicalBag.DispatchedAt = DateTimeOffset.UtcNow;
                await _bagRepository.UpdateAsync(physicalBag);
            }
        }
    }

    public async Task<int> ResetLoadingAsync(DateOnly date, int? routeId = null)
    {
        var board = await _packingService.GetPackingBoardAsync(date);
        var routes = board.Routes
            .Where(route => route.RouteId > 0 && route.TotalBags > 0)
            .Where(route => !routeId.HasValue || route.RouteId == routeId.Value)
            .ToList();

        if (routeId.HasValue && routes.Count == 0)
        {
            throw new InvalidOperationException($"Dostawa/trasa {routeId.Value} nie istnieje dla dnia {date:dd.MM.yyyy}.");
        }

        var resetBags = 0;
        var touchedSessions = new HashSet<int>();
        foreach (var bagDto in routes.SelectMany(route => route.Bags))
        {
            var session = await _sessionRepository.GetWithItemsAsync(bagDto.PackingSessionId);
            if (session is not null &&
                touchedSessions.Add(session.Id) &&
                session.Status is PackingStatus.Loaded or PackingStatus.Dispatched)
            {
                var oldStatus = session.Status;
                session.Status = PackingStatus.Labeled;
                await _sessionRepository.UpdateAsync(session);
                await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Reset zaladunku auta.");
            }

            var physicalBag = await _bagRepository.GetByIdAsync(bagDto.PackingBagId);
            if (physicalBag is null)
            {
                continue;
            }

            if (physicalBag.Status is PackingBagStatus.Manifested or PackingBagStatus.Loaded or PackingBagStatus.Dispatched)
            {
                physicalBag.Status = PackingBagStatus.Labeled;
                physicalBag.ManifestedAt = null;
                physicalBag.LoadedAt = null;
                physicalBag.LoadedBy = null;
                physicalBag.DispatchedAt = null;
                await _bagRepository.UpdateAsync(physicalBag);
                resetBags++;
            }
        }

        var routeIds = routes.Select(route => route.RouteId).ToHashSet();
        var manifests = (await _packingManifestRepository.GetAllAsync())
            .Where(manifest => manifest.PackingDate == date &&
                manifest.RouteId.HasValue &&
                routeIds.Contains(manifest.RouteId.Value) &&
                !manifest.IsSuperseded)
            .ToList();
        foreach (var manifest in manifests)
        {
            manifest.IsSuperseded = true;
            manifest.RequiresRegeneration = false;
            manifest.RequiresRegenerationReason = null;
            manifest.ChangeReason = "Reset zaladunku auta.";
            await _packingManifestRepository.UpdateAsync(manifest);
        }

        _logger.LogInformation(
            "Reset loading for {Date}, route {RouteId}. Reset {BagCount} bags and superseded {ManifestCount} manifests.",
            date,
            routeId,
            resetBags,
            manifests.Count);

        return resetBags;
    }

    /// <inheritdoc/>
    public async Task<PackingBagDto> LoadBagByCodeAsync(int routeId, string transportCode)
    {
        var normalizedCode = TransportLabelCodeNormalizer.Normalize(transportCode);
        PackingBag? physicalBag = await _bagRepository.GetByCodeAsync(normalizedCode);
        PackingLabel? shippingLabel = physicalBag is null
            ? null
            : await GetLatestShippingLabelForBagAsync(physicalBag.PackingSessionId, physicalBag.Id);

        if (shippingLabel is null)
        {
            shippingLabel = _packingLabelRepository is not null
                ? await _packingLabelRepository.GetShippingByQrCodeAsync(transportCode)
                    ?? await _packingLabelRepository.GetShippingByQrCodeAsync(normalizedCode)
                : await FindShippingLabelByCodeFallbackAsync(transportCode, normalizedCode);
        }

        if (shippingLabel is null)
        {
            if (int.TryParse(normalizedCode, out var directSessionId))
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

        physicalBag ??= await _bagRepository.GetDefaultForSessionAsync(session.Id);
        var latestShippingLabel = physicalBag is not null
            ? await GetLatestShippingLabelForBagAsync(session.Id, physicalBag.Id)
            : null;
        shippingLabel = latestShippingLabel ?? shippingLabel;
        if (!shippingLabel.AttachedAt.HasValue)
        {
            throw new InvalidOperationException("Torbe mozna zaladowac dopiero po potwierdzeniu przyklejenia etykiety transportowej.");
        }

        if (resolvedRouteId.Value != routeId)
        {
            var board = await _packingService.GetPackingBoardAsync(session.PackingDate);
            var correctRoute = board.Routes.FirstOrDefault(r => r.RouteId == resolvedRouteId.Value);
            var correctRouteName = correctRoute?.RouteName ?? $"Trasa #{resolvedRouteId}";
            throw new InvalidOperationException($"Błąd: Torba {transportCode} należy do innej trasy: {correctRouteName}.");
        }

        var manifest = await GetManifestAsync(session.PackingDate, routeId);
        EnsureManifestAllowsLoading(manifest);
        if (manifest is null)
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
        var bagDto = updatedRoute.Bags.FirstOrDefault(b => physicalBag is not null
                ? b.PackingBagId == physicalBag.Id
                : b.PackingSessionId == sessionId)
            ?? throw new InvalidOperationException($"Błąd przy aktualizacji danych torby #{sessionId}.");

        return bagDto;
    }

    /// <inheritdoc/>
    public async Task<PackingRouteDto?> GetRouteDetailsAsync(DateOnly date, int routeId)
    {
        var board = await _packingService.GetPackingBoardAsync(date);
        return board.Routes.FirstOrDefault(r => r.RouteId == routeId);
    }

    private static void EnsureManifestCanBeApproved(ManifestControlDto control, bool requireWorkerApproval)
    {
        if (control.Manifest is null)
        {
            throw new InvalidOperationException("Najpierw wygeneruj manifest dla tej dostawy.");
        }

        if (control.Manifest.RequiresRegeneration)
        {
            throw new InvalidOperationException(control.Manifest.RequiresRegenerationReason ?? "Manifest wymaga regeneracji.");
        }

        if (requireWorkerApproval && !control.Manifest.WorkerApprovedAt.HasValue)
        {
            throw new InvalidOperationException("Manifest musi najpierw zatwierdzić pracownik kompletacji.");
        }

        if (requireWorkerApproval && !control.Route.AllBagsLoaded)
        {
            throw new InvalidOperationException("Manifest finalny mozna zatwierdzic dopiero po zaladowaniu wszystkich toreb do auta.");
        }

        var incomplete = control.Checklist.FirstOrDefault(item => item.IsBlocking && !item.IsComplete);
        if (incomplete is not null)
        {
            throw new InvalidOperationException($"{incomplete.Label}: {incomplete.Details}");
        }

        var blockingIssue = control.Issues.FirstOrDefault(issue => issue.IsBlocking && !issue.IsResolved);
        if (blockingIssue is not null)
        {
            throw new InvalidOperationException($"{blockingIssue.Title}: {blockingIssue.Details}");
        }

        if (control.Manifest.IsVerified)
        {
            throw new InvalidOperationException("Manifest jest już finalnie zatwierdzony.");
        }
    }

    private static List<ManifestChecklistItemDto> BuildManifestChecklist(PackingRouteDto route, PackingManifestDto? manifest)
    {
        var hasAllLabels = route.TotalBags > 0 && route.Bags.All(bag => bag.HasLabels);
        var hasAllAttachedLabels = route.TotalBags > 0 && route.Bags.All(bag => bag.IsTransportLabelAttached);

        return new List<ManifestChecklistItemDto>
        {
            new()
            {
                Key = "packed",
                Label = "Torby spakowane",
                IsComplete = route.AllBagsPacked,
                Details = $"{route.PackedBags}/{route.TotalBags} toreb spakowanych.",
            },
            new()
            {
                Key = "labels",
                Label = "Etykiety wygenerowane",
                IsComplete = hasAllLabels,
                Details = hasAllLabels
                    ? "Każda torba ma etykietę transportową."
                    : "Brakuje etykiety transportowej dla co najmniej jednej torby.",
            },
            new()
            {
                Key = "attached",
                Label = "Etykiety przyklejone",
                IsComplete = hasAllAttachedLabels,
                Details = hasAllAttachedLabels
                    ? "Każda etykieta ma potwierdzenie przyklejenia."
                    : "Co najmniej jedna etykieta czeka na potwierdzenie przyklejenia.",
            },
            new()
            {
                Key = "manifest",
                Label = "Manifest wygenerowany",
                IsComplete = manifest is not null,
                Details = manifest is null
                    ? "Brak zapisanego manifestu dla tej trasy."
                    : $"Manifest {manifest.ManifestNumber}, wersja {manifest.ManifestVersion}.",
            },
            new()
            {
                Key = "route-consistency",
                Label = "Zgodność trasy i stopów",
                IsComplete = manifest is not null && !manifest.RequiresRegeneration,
                Details = manifest?.RequiresRegeneration == true
                    ? manifest.RequiresRegenerationReason ?? "Snapshot manifestu różni się od aktualnych danych."
                    : "Aktualne dane są zgodne ze snapshotem manifestu.",
            },
            new()
            {
                Key = "loading",
                Label = "Status załadunku",
                IsComplete = route.AllBagsLoaded,
                IsBlocking = false,
                Details = $"{route.LoadedBags}/{route.TotalBags} toreb załadowanych.",
            },
        };
    }

    private static void EnsureManifestAllowsLoading(PackingManifestDto? manifest)
    {
        if (manifest is null)
        {
            throw new InvalidOperationException("Torby mozna zaladowac dopiero po wygenerowaniu manifestu dostawy.");
        }

        if (manifest.RequiresRegeneration)
        {
            throw new InvalidOperationException(manifest.RequiresRegenerationReason ?? "Manifest wymaga regeneracji przed zaladunkiem.");
        }

        if (!manifest.WorkerApprovedAt.HasValue && !manifest.IsVerified)
        {
            throw new InvalidOperationException("Torby mozna zaladowac po zatwierdzeniu manifestu przez pracownika kompletacji.");
        }
    }

    private async Task<List<PackingManifestIssueDto>> BuildManifestIssuesAsync(
        DateOnly date,
        PackingRouteDto route,
        PackingManifestDto? manifest,
        IReadOnlyCollection<ManifestChecklistItemDto> checklist)
    {
        var issues = checklist
            .Where(item => item.IsBlocking && !item.IsComplete)
            .Select(item => new PackingManifestIssueDto
            {
                IssueType = item.Key,
                Title = item.Label,
                Details = item.Details,
                IsBlocking = true,
                Status = "Nowe",
            })
            .ToList();

        if (manifest?.RequiresRegeneration == true)
        {
            issues.Add(new PackingManifestIssueDto
            {
                IssueType = "manifest-regeneration",
                Title = "Manifest wymaga regeneracji",
                Details = manifest.RequiresRegenerationReason ?? "Dane trasy, toreb albo etykiet zmieniły się po wygenerowaniu manifestu.",
                IsBlocking = true,
                Status = "Nowe",
                SourceType = nameof(PackingManifest),
                SourceId = manifest.Id,
            });
        }

        if (_incidentRepository is not null)
        {
            var deliveryCalendarIds = route.Bags
                .Where(bag => bag.DeliveryCalendarId.HasValue)
                .Select(bag => bag.DeliveryCalendarId!.Value)
                .ToHashSet();
            var incidents = await _incidentRepository.SearchAsync(date, null, null, null, null);
            foreach (var incident in incidents.Where(incident =>
                incident.Status != PackingIncidentStatus.Resolved &&
                incident.DeliveryCalendarId.HasValue &&
                deliveryCalendarIds.Contains(incident.DeliveryCalendarId.Value)))
            {
                issues.Add(new PackingManifestIssueDto
                {
                    IssueType = "packing-incident",
                    Title = "Nierozwiązane zgłoszenie kompletacji",
                    Details = $"{incident.ClientPublicId ?? "-"} / DeliveryCalendarId {incident.DeliveryCalendarId}: {incident.Description}",
                    IsBlocking = true,
                    Status = MapIncidentStatus(incident.Status),
                    SourceType = nameof(PackingIncident),
                    SourceId = incident.Id,
                });
            }
        }

        if (_manifestIssueRepository is not null)
        {
            return await SynchronizeManifestIssuesAsync(date, route.RouteId, manifest?.Id, issues);
        }

        return issues;
    }

    private async Task EnsureRouteHasNoOpenPackingIncidentsAsync(DateOnly date, PackingRouteDto route)
    {
        if (_incidentRepository is null)
        {
            return;
        }

        var deliveryCalendarIds = route.Bags
            .Where(bag => bag.DeliveryCalendarId.HasValue)
            .Select(bag => bag.DeliveryCalendarId!.Value)
            .ToHashSet();
        if (deliveryCalendarIds.Count == 0)
        {
            return;
        }

        var incidents = await _incidentRepository.SearchAsync(date, null, null, null, null);
        var blockingIncident = incidents.FirstOrDefault(incident =>
            incident.Status != PackingIncidentStatus.Resolved &&
            incident.DeliveryCalendarId.HasValue &&
            deliveryCalendarIds.Contains(incident.DeliveryCalendarId.Value));

        if (blockingIncident is not null)
        {
            throw new InvalidOperationException(
                $"Manifest blokuje nierozwiązane zgłoszenie kompletacji #{blockingIncident.Id}: {blockingIncident.Description}");
        }
    }

    private async Task<List<PackingManifestIssueDto>> SynchronizeManifestIssuesAsync(
        DateOnly date,
        int routeId,
        int? manifestId,
        IReadOnlyCollection<PackingManifestIssueDto> calculatedIssues)
    {
        var persisted = (await _manifestIssueRepository!.GetByRouteAsync(date, routeId)).ToList();

        foreach (var issue in calculatedIssues)
        {
            var existing = persisted.FirstOrDefault(candidate =>
                IsSameManifestIssue(candidate, issue) &&
                candidate.Status is not PackingManifestIssueStatus.Resolved and not PackingManifestIssueStatus.AcceptedWithReason);

            if (existing is null)
            {
                var entity = new PackingManifestIssue
                {
                    PackingManifestId = manifestId,
                    PackingDate = date,
                    RouteId = routeId,
                    IssueType = issue.IssueType,
                    Status = PackingManifestIssueStatus.New,
                    IsBlocking = issue.IsBlocking,
                    Title = issue.Title,
                    Details = issue.Details,
                    SourceType = issue.SourceType,
                    SourceId = issue.SourceId,
                    ReportedAt = DateTimeOffset.UtcNow,
                };
                entity.Id = await _manifestIssueRepository.InsertAsync(entity);
                persisted.Add(entity);
                continue;
            }

            var shouldUpdate = existing.PackingManifestId != manifestId ||
                existing.IsBlocking != issue.IsBlocking ||
                !string.Equals(existing.Title, issue.Title, StringComparison.Ordinal) ||
                !string.Equals(existing.Details, issue.Details, StringComparison.Ordinal);

            if (shouldUpdate)
            {
                existing.PackingManifestId = manifestId;
                existing.IsBlocking = issue.IsBlocking;
                existing.Title = issue.Title;
                existing.Details = issue.Details;
                await _manifestIssueRepository.UpdateAsync(existing);
            }
        }

        var staleOpenIssues = persisted
            .Where(issue => issue.Status is not PackingManifestIssueStatus.Resolved and not PackingManifestIssueStatus.AcceptedWithReason)
            .Where(issue => !calculatedIssues.Any(calculated => IsSameManifestIssue(issue, calculated)))
            .ToList();
        foreach (var issue in staleOpenIssues)
        {
            issue.Status = PackingManifestIssueStatus.Resolved;
            issue.ResolvedAt = DateTimeOffset.UtcNow;
            issue.ResolvedByUserId = _currentUserService?.GetUserId();
            issue.ResolutionNotes = "Problem automatyczny nie wystepuje juz w aktualnej kontroli manifestu.";
            await _manifestIssueRepository.UpdateAsync(issue);
        }

        return persisted
            .Where(issue => issue.Status is not PackingManifestIssueStatus.Resolved and not PackingManifestIssueStatus.AcceptedWithReason ||
                calculatedIssues.Any(calculated => IsSameManifestIssue(issue, calculated)))
            .Select(MapManifestIssue)
            .ToList();
    }

    private static bool IsSameManifestIssue(PackingManifestIssue issue, PackingManifestIssueDto calculated)
    {
        return string.Equals(issue.IssueType, calculated.IssueType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.SourceType, calculated.SourceType, StringComparison.OrdinalIgnoreCase) &&
            issue.SourceId == calculated.SourceId;
    }

    private static string MapIncidentStatus(PackingIncidentStatus status)
    {
        return status switch
        {
            PackingIncidentStatus.InProgress => "W trakcie",
            PackingIncidentStatus.KitchenReworkRequested => "W trakcie",
            PackingIncidentStatus.WarehouseActionRequired => "W trakcie",
            PackingIncidentStatus.Resolved => "Rozwiązane",
            _ => "Nowe",
        };
    }

    private static PackingManifestIssueDto MapManifestIssue(PackingManifestIssue issue)
    {
        return new PackingManifestIssueDto
        {
            IssueType = issue.IssueType,
            Title = issue.Title,
            Details = issue.Details,
            IsBlocking = issue.IsBlocking,
            Status = issue.Status switch
            {
                PackingManifestIssueStatus.InProgress => "W trakcie",
                PackingManifestIssueStatus.Resolved => "Rozwiązane",
                PackingManifestIssueStatus.AcceptedWithReason => "Zaakceptowane z uzasadnieniem",
                _ => "Nowe",
            },
            IsResolved = issue.Status is PackingManifestIssueStatus.Resolved or PackingManifestIssueStatus.AcceptedWithReason,
            SourceType = issue.SourceType,
            SourceId = issue.SourceId,
        };
    }

    private static string BuildManifestSnapshotHash(PackingRouteDto route, IReadOnlyList<PackingLabel> shippingLabels)
    {
        var snapshot = new
        {
            route.RouteId,
            route.RouteName,
            route.VehicleId,
            route.VehicleRegistration,
            bags = route.Bags
                .OrderBy(bag => bag.StopNumber)
                .ThenBy(bag => bag.BagNumber)
                .Select(bag =>
                {
                    var label = FindLatestShippingLabel(shippingLabels, bag);
                    return new
                    {
                        bag.PackingSessionId,
                        bag.PackingBagId,
                        bag.BagCode,
                        bag.DeliveryCalendarId,
                        bag.StopNumber,
                        bag.OrderId,
                        bag.TotalBoxes,
                        transportLabelId = label?.Id,
                        transportQrCode = label?.QrCode,
                        transportPrintNumber = label?.PrintNumber,
                        transportAttachedAt = label?.AttachedAt,
                    };
                }),
        };
        var json = JsonSerializer.Serialize(snapshot);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    private async Task<PackingManifest?> GetLatestPackingManifestEntityAsync(DateOnly date, int routeId)
    {
        if (_packingManifestQueryRepository is not null)
        {
            return await _packingManifestQueryRepository.GetLatestAsync(date, routeId);
        }

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

    private async Task<IReadOnlyList<PackingLabel>> GetShippingLabelsForRouteAsync(PackingRouteDto route)
    {
        var bagIds = route.Bags.Select(bag => bag.PackingBagId).Where(id => id > 0).Distinct().ToArray();
        if (_packingLabelRepository is not null)
        {
            return await _packingLabelRepository.GetShippingForBagsAsync(bagIds);
        }

        var labels = await _labelRepository.GetAllAsync();
        return labels
            .Where(label => label.LabelType == LabelType.Shipping)
            .Where(label => label.PackingBagId.HasValue && bagIds.Contains(label.PackingBagId.Value))
            .ToList();
    }

    private async Task<PackingLabel?> GetLatestShippingLabelForBagAsync(int packingSessionId, int packingBagId)
    {
        if (_packingLabelRepository is not null)
        {
            return await _packingLabelRepository.GetLatestShippingForBagAsync(packingBagId);
        }

        var labels = await _labelRepository.GetAllAsync();
        return labels
            .Where(label => label.LabelType == LabelType.Shipping &&
                (label.PackingBagId == packingBagId || label.PackingSessionId == packingSessionId))
            .OrderByDescending(label => label.PrintNumber)
            .ThenByDescending(label => label.Id)
            .FirstOrDefault();
    }

    private async Task<PackingLabel?> FindShippingLabelByCodeFallbackAsync(string transportCode, string normalizedCode)
    {
        var labels = await _labelRepository.GetAllAsync();
        return labels
            .Where(label => label.LabelType == LabelType.Shipping)
            .FirstOrDefault(label =>
                string.Equals(label.QrCode, transportCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label.QrCode, normalizedCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(TransportLabelCodeNormalizer.Normalize(label.QrCode), normalizedCode, StringComparison.OrdinalIgnoreCase));
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

            if (!bag.IsTransportLabelAttached)
            {
                throw new InvalidOperationException($"Torba #{bag.PackingSessionId} nie ma potwierdzonego przyklejenia etykiety transportowej.");
            }

            var currentLabel = FindLatestShippingLabel(shippingLabels, bag)
                ?? throw new InvalidOperationException($"Torba #{bag.PackingSessionId} nie ma etykiety transportowej.");

            if (!currentLabel.AttachedAt.HasValue)
            {
                throw new InvalidOperationException($"Torba #{bag.PackingSessionId} nie ma potwierdzonego przyklejenia etykiety transportowej.");
            }

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
                    transportAttachedAt = labelsBySession.TryGetValue(bag.PackingSessionId, out var attachedLabel)
                        ? attachedLabel.AttachedAt
                        : null,
                    transportAttachedBy = labelsBySession.TryGetValue(bag.PackingSessionId, out var attachedByLabel)
                        ? attachedByLabel.AttachedBy
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
