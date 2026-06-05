using AutoMapper;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Domain.Interfaces.Production;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KuchniaUCygana.Application.Services;

/// <summary>
/// Serwis kompletacji: jedna PackingSession reprezentuje jedną torbę zamówienia.
/// </summary>
public sealed class PackingService : IPackingService
{
    private readonly IPackingSessionRepository sessionRepository;
    private readonly IRepository<PackingItem> itemRepository;
    private readonly IRepository<PackingLabel> labelRepository;
    private readonly IRepository<PackingManifest> packingManifestRepository;
    private readonly IPackingLabelRepository? packingLabelRepository;
    private readonly IPackingManifestRepository? packingManifestQueryRepository;
    private readonly IPackingBagRepository bagRepository;
    private readonly IBoxLabelRepository boxLabelRepository;
    private readonly IPackingStatusLogRepository statusLogRepository;
    private readonly IOrderDataProvider orderProvider;
    private readonly IDietDataProvider dietProvider;
    private readonly IDeliveryManifestProvider manifestProvider;
    private readonly IApplicationUrlProvider applicationUrlProvider;
    private readonly ICurrentUserService currentUserService;
    private readonly IProductionPlanRepository productionPlanRepository;
    private readonly IRepository<ProductionPlanItem> productionPlanItemRepository;
    private readonly IMapper mapper;
    private readonly ILogger<PackingService> logger;

    public PackingService(
        IPackingSessionRepository sessionRepository,
        IRepository<PackingItem> itemRepository,
        IRepository<PackingLabel> labelRepository,
        IRepository<PackingManifest> packingManifestRepository,
        IPackingBagRepository bagRepository,
        IBoxLabelRepository boxLabelRepository,
        IPackingStatusLogRepository statusLogRepository,
        IOrderDataProvider orderProvider,
        IDietDataProvider dietProvider,
        IDeliveryManifestProvider manifestProvider,
        IApplicationUrlProvider applicationUrlProvider,
        ICurrentUserService currentUserService,
        IProductionPlanRepository productionPlanRepository,
        IRepository<ProductionPlanItem> productionPlanItemRepository,
        IMapper mapper,
        ILogger<PackingService> logger,
        IPackingLabelRepository? packingLabelRepository = null,
        IPackingManifestRepository? packingManifestQueryRepository = null)
    {
        this.sessionRepository = sessionRepository;
        this.itemRepository = itemRepository;
        this.labelRepository = labelRepository;
        this.packingManifestRepository = packingManifestRepository;
        this.bagRepository = bagRepository;
        this.boxLabelRepository = boxLabelRepository;
        this.statusLogRepository = statusLogRepository;
        this.orderProvider = orderProvider;
        this.dietProvider = dietProvider;
        this.manifestProvider = manifestProvider;
        this.applicationUrlProvider = applicationUrlProvider;
        this.currentUserService = currentUserService;
        this.productionPlanRepository = productionPlanRepository;
        this.productionPlanItemRepository = productionPlanItemRepository;
        this.mapper = mapper;
        this.logger = logger;
        this.packingLabelRepository = packingLabelRepository;
        this.packingManifestQueryRepository = packingManifestQueryRepository;
    }

    public async Task<PackingBoardDto> GetPackingBoardAsync(DateOnly date)
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

        var routes = (await manifestProvider.GetRoutesForDateAsync(date))
            .OrderBy(r => r.RouteId)
            .ToList();

        var assignments = AssignOrdersToRoutes(activeOrders, deliveries, routes);
        if (assignments.Any(a => a.Route.RouteId == 0) && routes.All(r => r.RouteId != 0))
        {
            routes.Add(assignments.First(a => a.Route.RouteId == 0).Route);
        }

        var sessions = (await sessionRepository.GetByDateWithItemsAsync(date))
            .Where(s => s.DeliveryCalendarId.HasValue || s.OrderId.HasValue)
            .GroupBy(s => s.DeliveryCalendarId ?? s.OrderId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var sessionIds = sessions.Values.Select(s => s.Id).ToArray();
        var bagsBySession = (await bagRepository.GetBySessionIdsAsync(sessionIds))
            .GroupBy(b => b.PackingSessionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.BagNumber).ThenBy(b => b.Id).ToList());
        var latestLabelsByBag = await GetLatestShippingLabelsByBagAsync(
            bagsBySession.Values.SelectMany(bags => bags).Select(bag => bag.Id));

        var board = new PackingBoardDto { PackingDate = date };
        var latestManifestsByRoute = await GetLatestManifestsByRouteAsync(date, routes.Select(route => route.RouteId));

        foreach (var route in routes)
        {
            var routeDto = new PackingRouteDto
            {
                RouteId = route.RouteId,
                RouteName = route.RouteName,
                VehicleId = route.VehicleId,
                VehicleRegistration = route.VehicleRegistration,
            };

            foreach (var assignment in assignments.Where(a => a.Route.RouteId == route.RouteId))
            {
                var sessionKey = assignment.DeliveryCalendarId > 0
                    ? assignment.DeliveryCalendarId
                    : assignment.Order.OrderId;

                if (!sessions.TryGetValue(sessionKey, out var session))
                {
                    continue;
                }

                if (!bagsBySession.TryGetValue(session.Id, out var physicalBags))
                {
                    continue;
                }

                foreach (var physicalBag in physicalBags)
                {
                    latestLabelsByBag.TryGetValue(physicalBag.Id, out var transportLabel);
                    routeDto.Bags.Add(CreateBagDto(session, physicalBag, assignment, transportLabel));
                }
            }

            routeDto.Bags = routeDto.Bags
                .OrderBy(b => b.StopNumber)
                .ThenBy(b => b.OrderId)
                .ToList();
            routeDto.TotalBags = routeDto.Bags.Count;
            routeDto.PackedBags = routeDto.Bags.Count(b => b.Status is nameof(PackingBagStatus.Packed) or nameof(PackingBagStatus.Labeled) or nameof(PackingBagStatus.Manifested) or nameof(PackingBagStatus.Loaded) or nameof(PackingBagStatus.Dispatched));
            routeDto.LoadedBags = routeDto.Bags.Count(b => b.Status is nameof(PackingBagStatus.Loaded) or nameof(PackingBagStatus.Dispatched));
            routeDto.DispatchedBags = routeDto.Bags.Count(b => b.Status == nameof(PackingBagStatus.Dispatched));
            routeDto.AllBagsPacked = routeDto.TotalBags > 0 && routeDto.PackedBags == routeDto.TotalBags;
            routeDto.AllBagsLoaded = routeDto.TotalBags > 0 && routeDto.LoadedBags == routeDto.TotalBags;
            var allBagsReadyForManifest = routeDto.TotalBags > 0 &&
                routeDto.Bags.All(b => b.HasLabels && b.IsTransportLabelAttached);

            if (latestManifestsByRoute.TryGetValue(route.RouteId, out var manifest))
            {
                routeDto.HasManifest = true;
                routeDto.ManifestId = manifest.Id;
                routeDto.ManifestNumber = manifest.ManifestNumber;
                routeDto.ManifestGeneratedAt = manifest.GeneratedAt;
                routeDto.IsManifestVerified = manifest.IsVerified;
                routeDto.ManifestVerifiedAt = manifest.VerifiedAt;
                routeDto.ManifestWorkerApprovedAt = manifest.WorkerApprovedAt;
                routeDto.ManifestSentToLogisticsAt = manifest.SentToLogisticsAt;
                routeDto.ManifestRequiresRegeneration = manifest.RequiresRegeneration;
                routeDto.ManifestRequiresRegenerationReason = manifest.RequiresRegenerationReason;
            }

            routeDto.CanGenerateManifest = route.RouteId > 0 && routeDto.AllBagsPacked && allBagsReadyForManifest;
            routeDto.CanWorkerApproveManifest = routeDto.HasManifest &&
                !routeDto.IsManifestVerified &&
                !routeDto.ManifestWorkerApprovedAt.HasValue &&
                !routeDto.ManifestRequiresRegeneration &&
                routeDto.AllBagsPacked &&
                allBagsReadyForManifest;
            routeDto.CanSupervisorApproveManifest = routeDto.HasManifest &&
                !routeDto.IsManifestVerified &&
                !routeDto.ManifestRequiresRegeneration &&
                routeDto.ManifestWorkerApprovedAt.HasValue &&
                routeDto.AllBagsLoaded &&
                allBagsReadyForManifest;
            routeDto.CanVerifyManifest = routeDto.CanSupervisorApproveManifest;
            routeDto.CanLoadBags = routeDto.HasManifest &&
                !routeDto.ManifestRequiresRegeneration &&
                (routeDto.ManifestWorkerApprovedAt.HasValue || routeDto.IsManifestVerified) &&
                allBagsReadyForManifest;
            routeDto.CanDispatchDelivery = routeDto.IsManifestVerified && routeDto.AllBagsLoaded;

            board.Routes.Add(routeDto);
        }

        board.TotalBags = board.Routes.Sum(r => r.TotalBags);
        board.PackedBags = board.Routes.Sum(r => r.PackedBags);
        board.LoadedBags = board.Routes.Sum(r => r.LoadedBags);

        return board;
    }

    public async Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy)
    {
        await GetPackingBoardAsync(date);
        var firstSession = (await sessionRepository.GetByDateWithItemsAsync(date)).FirstOrDefault()
            ?? throw new InvalidOperationException($"Brak zamówień do pakowania na {date:dd.MM.yyyy}.");

        firstSession.PackedBy = string.IsNullOrWhiteSpace(firstSession.PackedBy)
            ? packedBy
            : firstSession.PackedBy;
        await sessionRepository.UpdateAsync(firstSession);

        return MapSession(firstSession);
    }

    public async Task<IEnumerable<PackingItemDto>> PrepareOrderBoxesAsync(int packingSessionId)
    {
        var session = await sessionRepository.GetWithItemsAsync(packingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {packingSessionId} nie istnieje.");

        if (session.OrderId is null)
        {
            throw new InvalidOperationException($"Torba #{packingSessionId} nie jest przypisana do zamówienia.");
        }

        if (session.Items.Count > 0)
        {
            return session.Items
                .OrderBy(i => i.Id)
                .Select(item => MapPackingItemForSession(item, session))
                .ToList();
        }

        var boxDefinitions = (await BuildBoxDefinitionsAsync(session)).ToList();
        var created = new List<PackingItem>();
        var sequence = 1;
        var defaultBag = await EnsureDefaultBagAsync(session.Id);

        foreach (var definition in boxDefinitions)
        {
            var item = new PackingItem
            {
                PackingSessionId = session.Id,
                PackingBagId = defaultBag.Id,
                MealId = definition.MealId,
                MealName = definition.MealName,
                DietVariantId = definition.DietVariantId,
                ProductionPlanItemId = definition.ProductionPlanItemId,
                BoxCode = CreateBoxCode(session, sequence),
                Status = PackingItemStatus.Pending,
                IsDamaged = false,
            };

            var id = await itemRepository.InsertAsync(item);
            item.Id = id;
            created.Add(item);
            sequence++;
        }

        logger.LogInformation(
            "Przygotowano {Count} pudełek dla torby {SessionId} zamówienia {OrderId}",
            created.Count,
            session.Id,
            session.OrderId);

        return created
            .OrderBy(i => i.Id)
            .Select(item => MapPackingItemForSession(item, session))
            .ToList();
    }

    public async Task MarkBoxPackedAsync(int packingItemId, string packedBy)
    {
        var item = await itemRepository.GetByIdAsync(packingItemId)
            ?? throw new InvalidOperationException($"Pudełko {packingItemId} nie istnieje.");

        var session = await sessionRepository.GetWithItemsAsync(item.PackingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {item.PackingSessionId} nie istnieje.");

        var result = await PackBoxInternalAsync(session, item, packedBy);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }

    }

    public async Task PackOrderBagAsync(int packingSessionId, string packedBy)
    {
        var session = await sessionRepository.GetWithItemsAsync(packingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {packingSessionId} nie istnieje.");

        if (session.Status is PackingStatus.Loaded or PackingStatus.Dispatched)
        {
            throw new InvalidOperationException("Torba jest już w procesie załadunku albo wysyłki.");
        }

        if (session.Items.Count == 0)
        {
            throw new InvalidOperationException("Nie można zamknąć torby bez pudełek utworzonych i zaakceptowanych przez kuchnię.");
        }

        var activeItems = GetActiveItems(session).ToList();
        if (activeItems.Count == 0 || activeItems.Any(i => i.Status != PackingItemStatus.Packed))
        {
            throw new InvalidOperationException("Najpierw spakuj wszystkie pudełka w torbie.");
        }

        var oldStatus = session.Status;
        session.Status = PackingStatus.Packed;
        session.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? session.PackedBy : packedBy;
        await sessionRepository.UpdateAsync(session);
        await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Torba oznaczona jako spakowana.");

        var defaultBag = await EnsureDefaultBagAsync(session.Id);
        if (defaultBag.Status == PackingBagStatus.Pending)
        {
            defaultBag.Status = PackingBagStatus.Packed;
            defaultBag.PackedAt = DateTimeOffset.UtcNow;
            defaultBag.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? "System" : packedBy;
            await bagRepository.UpdateAsync(defaultBag);
        }
    }

    public async Task<ScanBoxResponse> ScanBoxAsync(int sessionId, string barcode, string packedBy)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {sessionId} nie istnieje.");

        var scannedCode = barcode.Trim();
        var label = await boxLabelRepository.GetByQrCodeAsync(scannedCode);
        if (label is null)
        {
            return CreateScanBoxResponse(
                session,
                null,
                false,
                $"Błąd: Kod {scannedCode} nie jest ważną etykietą produktową.");
        }

        var item = label is null
            ? null
            : session.Items.FirstOrDefault(i => i.Id == label.PackingItemId);

        if (item is null)
        {
            return CreateScanBoxResponse(
                session,
                null,
                false,
                $"Błąd: Kod {scannedCode} należy do innej dostawy.");
        }

        return await PackBoxInternalAsync(session, item, packedBy);
    }

    public async Task<PackingItemDto> ReportPackingItemIssueAsync(ReportPackingItemIssueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Powód zgłoszenia uszkodzenia albo braku pudełka jest wymagany.");
        }

        var issueStatus = request.IssueType.Trim().Equals(nameof(PackingItemStatus.Missing), StringComparison.OrdinalIgnoreCase)
            ? PackingItemStatus.Missing
            : request.IssueType.Trim().Equals(nameof(PackingItemStatus.Damaged), StringComparison.OrdinalIgnoreCase)
                ? PackingItemStatus.Damaged
                : throw new InvalidOperationException("Nieznany typ problemu pudełka.");

        var item = await itemRepository.GetByIdAsync(request.PackingItemId)
            ?? throw new InvalidOperationException($"Pudełko {request.PackingItemId} nie istnieje.");
        var session = await sessionRepository.GetWithItemsAsync(item.PackingSessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {item.PackingSessionId} nie istnieje.");

        EnsureSessionCanBeEdited(session);

        if (item.Status is PackingItemStatus.Damaged or PackingItemStatus.Missing)
        {
            throw new InvalidOperationException("Pudełko ma już zgłoszony problem.");
        }

        var activeBag = await EnsureDefaultBagAsync(session.Id);
        item.Status = issueStatus;
        item.IsDamaged = issueStatus == PackingItemStatus.Damaged;
        item.Remarks = request.Reason.Trim();
        item.IssueReportedAt = DateTimeOffset.UtcNow;
        item.IssueReportedByUserId = currentUserService.GetUserId();
        await itemRepository.UpdateAsync(item);

        var replacement = new PackingItem
        {
            PackingSessionId = session.Id,
            PackingBagId = activeBag.Id,
            ProductionPlanItemId = item.ProductionPlanItemId,
            MealId = item.MealId,
            MealName = item.MealName,
            DietVariantId = item.DietVariantId,
            BatchId = item.BatchId,
            BoxCode = CreateBoxCode(session, session.Items.Count + 1),
            Status = PackingItemStatus.Pending,
            ReplacementForPackingItemId = item.Id,
            ExpiryDate = item.ExpiryDate,
        };

        replacement.Id = await itemRepository.InsertAsync(replacement);
        session.Items.Add(replacement);

        var oldStatus = session.Status;
        session.Status = PackingStatus.Pending;
        await sessionRepository.UpdateAsync(session);
        await LogStatusChangeAsync(session.Id, oldStatus, session.Status, $"Zgłoszono {issueStatus} pudełka {item.BoxCode}. Utworzono zamiennik {replacement.BoxCode}.");

        return MapPackingItemForSession(replacement, session);
    }

    public async Task<PackingBagDto> ReportPackingBagDamageAsync(ReportPackingBagDamageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Powód zgłoszenia uszkodzenia torby jest wymagany.");
        }

        var damagedBag = await bagRepository.GetByIdAsync(request.PackingBagId)
            ?? throw new InvalidOperationException($"Torba fizyczna {request.PackingBagId} nie istnieje.");
        var session = await sessionRepository.GetWithItemsAsync(damagedBag.PackingSessionId)
            ?? throw new InvalidOperationException($"Sesja pakowania {damagedBag.PackingSessionId} nie istnieje.");

        if (session.Status is PackingStatus.Loaded or PackingStatus.Dispatched ||
            damagedBag.Status is PackingBagStatus.Loaded or PackingBagStatus.Dispatched)
        {
            throw new InvalidOperationException("Nie można wymienić torby po załadunku lub wysyłce.");
        }

        var board = await GetPackingBoardAsync(session.PackingDate);
        var route = board.Routes.FirstOrDefault(r => r.Bags.Any(b => b.PackingSessionId == session.Id));
        if (route?.IsManifestVerified == true && !request.AllowVerifiedManifestChange)
        {
            throw new InvalidOperationException("Zweryfikowany manifest moze zmienic tylko PackingManager albo Admin.");
        }

        if (route?.HasManifest == true && route.ManifestId.HasValue)
        {
            var manifest = await packingManifestRepository.GetByIdAsync(route.ManifestId.Value);
            if (manifest is not null)
            {
                manifest.IsSuperseded = true;
                manifest.ChangeReason = $"Wymiana uszkodzonej torby {damagedBag.BagCode}: {request.Reason.Trim()}";
                await packingManifestRepository.UpdateAsync(manifest);
            }
        }

        var allBags = (await bagRepository.GetBySessionIdAsync(session.Id)).ToList();
        var nextBagNumber = allBags.Count == 0 ? 1 : allBags.Max(b => b.BagNumber) + 1;
        var replacementBag = new PackingBag
        {
            PackingSessionId = session.Id,
            BagNumber = nextBagNumber,
            BagCode = $"BAG-{session.Id:D6}-{nextBagNumber:D2}",
            Status = PackingBagStatus.Pending,
        };
        replacementBag.Id = await bagRepository.InsertAsync(replacementBag);

        damagedBag.Status = PackingBagStatus.Damaged;
        damagedBag.DamageReason = request.Reason.Trim();
        damagedBag.DamagedAt = DateTimeOffset.UtcNow;
        damagedBag.DamagedByUserId = currentUserService.GetUserId();
        damagedBag.ReplacementPackingBagId = replacementBag.Id;
        await bagRepository.UpdateAsync(damagedBag);

        foreach (var item in GetActiveItems(session))
        {
            item.PackingBagId = replacementBag.Id;
            if (item.Status == PackingItemStatus.Packed)
            {
                item.Status = PackingItemStatus.FoilPrinted;
                item.PackedAt = null;
                item.PackedBy = null;
            }

            await itemRepository.UpdateAsync(item);
        }

        var oldStatus = session.Status;
        session.Status = PackingStatus.Pending;
        await sessionRepository.UpdateAsync(session);
        await LogStatusChangeAsync(session.Id, oldStatus, session.Status, $"Wymieniono uszkodzoną torbę {damagedBag.BagCode} na {replacementBag.BagCode}.");

        var refreshedBoard = await GetPackingBoardAsync(session.PackingDate);
        return refreshedBoard.Routes
            .SelectMany(r => r.Bags)
            .First(b => b.PackingBagId == replacementBag.Id);
    }

    public async Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsAsync(
        int sessionId,
        string? reprintReason = null,
        bool forceNewPrint = false)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {sessionId} nie istnieje.");

        if (session.Status != PackingStatus.Packed && session.Status != PackingStatus.Labeled && session.Status != PackingStatus.Loaded && session.Status != PackingStatus.Dispatched)
        {
            throw new InvalidOperationException("Najpierw zamknij torbę po spakowaniu wszystkich pudełek, a dopiero potem wydrukuj etykietę transportową.");
        }

        var board = await GetPackingBoardAsync(session.PackingDate);
        var bag = board.Routes
            .SelectMany(r => r.Bags)
            .FirstOrDefault(b => b.PackingSessionId == sessionId);

        if (bag is null || bag.RouteId <= 0)
        {
            throw new InvalidOperationException("Brak trasy M4 dla tej dostawy. Można pakować pudełka, ale etykieta transportowa wymaga stopu z DeliveryCalendarId.");
        }

        var routeBag = bag;

        var physicalBag = await EnsureDefaultBagAsync(session.Id);
        var shippingLabel = await GetLatestShippingLabelAsync(sessionId, physicalBag.Id);
        var existingPrintCount = await GetShippingLabelPrintCountAsync(sessionId, physicalBag.Id);

        if (forceNewPrint && existingPrintCount > 0 && string.IsNullOrWhiteSpace(reprintReason))
        {
            throw new InvalidOperationException("Powód redruku etykiety transportowej jest wymagany.");
        }

        if (shippingLabel is null || forceNewPrint)
        {
            var printNumber = existingPrintCount + 1;
            if (printNumber > 1 && string.IsNullOrWhiteSpace(reprintReason))
            {
                throw new InvalidOperationException("Powód redruku etykiety transportowej jest wymagany.");
            }

            shippingLabel = new PackingLabel
            {
                PackingSessionId = sessionId,
                PackingBagId = physicalBag.Id,
                LabelType = LabelType.Shipping,
                QrCode = BuildTransportQrCode(physicalBag.BagCode),
                ClientName = null,
                RouteInfo = BuildRouteInfo(routeBag),
                DeliveryWindow = routeBag.DeliveryWindow,
                MealsList = BuildMealsList(session),
                PrintNumber = printNumber,
                ReprintReason = printNumber > 1 ? reprintReason!.Trim() : null,
                PrintedAt = DateTimeOffset.UtcNow,
                PrintedBy = "Packing",
            };
            shippingLabel.LabelDataJson = BuildTransportLabelSnapshot(shippingLabel, physicalBag, routeBag);

            var id = await labelRepository.InsertAsync(shippingLabel);
            shippingLabel.Id = id;
        }

        await SynchronizeTransportLabelWithRouteAsync(shippingLabel, physicalBag, routeBag, BuildMealsList(session));

        if (session.Status == PackingStatus.Packed)
        {
            var oldStatus = session.Status;
            session.Status = PackingStatus.Labeled;
            await sessionRepository.UpdateAsync(session);
            await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Wydrukowano etykietę transportową.");
        }

        if (physicalBag.Status is PackingBagStatus.Packed or PackingBagStatus.Pending)
        {
            physicalBag.Status = PackingBagStatus.Labeled;
            physicalBag.LabeledAt = DateTimeOffset.UtcNow;
            await bagRepository.UpdateAsync(physicalBag);
        }

        logger.LogInformation("Wygenerowano lub odczytano etykietę transportową dla torby {SessionId}", sessionId);

        return new[] { CreateTransportLabelDto(shippingLabel, physicalBag, routeBag) };
    }

    public async Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsForBagAsync(
        int packingBagId,
        string? reprintReason = null,
        bool forceNewPrint = false)
    {
        var physicalBag = await bagRepository.GetByIdAsync(packingBagId)
            ?? throw new InvalidOperationException($"Torba fizyczna {packingBagId} nie istnieje.");
        var session = await sessionRepository.GetWithItemsAsync(physicalBag.PackingSessionId)
            ?? throw new InvalidOperationException($"Sesja pakowania {physicalBag.PackingSessionId} nie istnieje.");

        if (physicalBag.Status is not (PackingBagStatus.Packed or PackingBagStatus.Labeled or PackingBagStatus.Manifested or PackingBagStatus.Loaded or PackingBagStatus.Dispatched))
        {
            throw new InvalidOperationException("Najpierw zamknij torbę po spakowaniu wszystkich pudełek, a dopiero potem wydrukuj etykietę transportową.");
        }

        var board = await GetPackingBoardAsync(session.PackingDate);
        var routeBag = board.Routes
            .SelectMany(r => r.Bags)
            .FirstOrDefault(b => b.PackingBagId == physicalBag.Id);

        if (routeBag is null || routeBag.RouteId <= 0)
        {
            throw new InvalidOperationException("Brak trasy M4 dla tej dostawy. Można pakować pudełka, ale etykieta transportowa wymaga stopu z DeliveryCalendarId.");
        }

        var shippingLabel = await GetLatestShippingLabelAsync(session.Id, physicalBag.Id);
        var existingPrintCount = await GetShippingLabelPrintCountAsync(session.Id, physicalBag.Id);

        if (forceNewPrint && existingPrintCount > 0 && string.IsNullOrWhiteSpace(reprintReason))
        {
            throw new InvalidOperationException("Powód redruku etykiety transportowej jest wymagany.");
        }

        if (shippingLabel is null || forceNewPrint)
        {
            var printNumber = existingPrintCount + 1;
            if (printNumber > 1 && string.IsNullOrWhiteSpace(reprintReason))
            {
                throw new InvalidOperationException("Powód redruku etykiety transportowej jest wymagany.");
            }

            shippingLabel = new PackingLabel
            {
                PackingSessionId = session.Id,
                PackingBagId = physicalBag.Id,
                LabelType = LabelType.Shipping,
                QrCode = BuildTransportQrCode(physicalBag.BagCode),
                ClientName = null,
                RouteInfo = BuildRouteInfo(routeBag),
                DeliveryWindow = routeBag.DeliveryWindow,
                MealsList = BuildMealsList(session),
                PrintNumber = printNumber,
                ReprintReason = printNumber > 1 ? reprintReason!.Trim() : null,
                PrintedAt = DateTimeOffset.UtcNow,
                PrintedBy = currentUserService.GetUserName() ?? "Packing",
            };
            shippingLabel.LabelDataJson = BuildTransportLabelSnapshot(shippingLabel, physicalBag, routeBag);

            shippingLabel.Id = await labelRepository.InsertAsync(shippingLabel);
        }

        await SynchronizeTransportLabelWithRouteAsync(shippingLabel, physicalBag, routeBag, BuildMealsList(session));

        if (session.Status == PackingStatus.Packed)
        {
            var oldStatus = session.Status;
            session.Status = PackingStatus.Labeled;
            await sessionRepository.UpdateAsync(session);
            await LogStatusChangeAsync(session.Id, oldStatus, session.Status, "Wydrukowano etykietę transportową.");
        }

        if (physicalBag.Status is PackingBagStatus.Packed or PackingBagStatus.Pending)
        {
            physicalBag.Status = PackingBagStatus.Labeled;
            physicalBag.LabeledAt = DateTimeOffset.UtcNow;
            await bagRepository.UpdateAsync(physicalBag);
        }

        logger.LogInformation("Wygenerowano lub odczytano etykietę transportową dla torby {PackingBagId}", physicalBag.Id);

        return new[] { CreateTransportLabelDto(shippingLabel, physicalBag, routeBag) };
    }

    public Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId)
    {
        return GenerateTransportLabelsAsync(sessionId);
    }

    public async Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForSessionAsync(int sessionId)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId)
            ?? throw new InvalidOperationException($"Torba pakowania {sessionId} nie istnieje.");
        var labels = packingLabelRepository is not null
            ? await packingLabelRepository.GetShippingForSessionAsync(sessionId)
            : (await labelRepository.GetAllAsync())
                .Where(label => label.LabelType == LabelType.Shipping && label.PackingSessionId == sessionId)
                .OrderByDescending(label => label.PrintNumber)
                .ThenByDescending(label => label.Id)
                .ToList();

        var result = new List<PackingLabelDto>();
        foreach (var label in labels)
        {
            result.Add(await CreateTransportLabelDtoAsync(label));
        }

        return result;
    }

    public async Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForRouteAsync(DateOnly date, int routeId)
    {
        var board = await GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);
        var bagIds = route.Bags.Select(bag => bag.PackingBagId).ToArray();
        var labels = packingLabelRepository is not null
            ? await packingLabelRepository.GetShippingForBagsAsync(bagIds)
            : (await labelRepository.GetAllAsync())
                .Where(label => label.LabelType == LabelType.Shipping && label.PackingBagId.HasValue && bagIds.Contains(label.PackingBagId.Value))
                .OrderBy(label => label.PackingBagId)
                .ThenByDescending(label => label.PrintNumber)
                .ThenByDescending(label => label.Id)
                .ToList();

        var latestByBag = labels
            .Where(label => label.PackingBagId.HasValue)
            .GroupBy(label => label.PackingBagId!.Value)
            .Select(group => group.OrderByDescending(label => label.PrintNumber).ThenByDescending(label => label.Id).First())
            .OrderBy(label => route.Bags.FindIndex(bag => bag.PackingBagId == label.PackingBagId))
            .ToList();

        var result = new List<PackingLabelDto>();
        foreach (var label in latestByBag)
        {
            result.Add(await CreateTransportLabelDtoAsync(label));
        }

        return result;
    }

    public async Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForDeliveryAsync(
        DateOnly date,
        int routeId,
        string? reprintReason = null,
        bool forceNewPrint = false)
    {
        var board = await GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);
        var labels = new List<PackingLabelDto>();

        foreach (var bag in route.Bags)
        {
            labels.AddRange(await GenerateTransportLabelsAsync(
                bag.PackingSessionId,
                reprintReason,
                forceNewPrint));
        }

        return labels;
    }

    public async Task<IReadOnlyList<PackingLabelDto>> GenerateMissingTransportLabelsForRouteAsync(DateOnly date, int routeId)
    {
        var board = await GetPackingBoardAsync(date);
        var route = GetRouteOrThrow(board, routeId);
        if (route.TotalBags == 0)
        {
            throw new InvalidOperationException("Trasa nie ma toreb do etykietowania.");
        }

        if (!route.AllBagsPacked)
        {
            throw new InvalidOperationException("Etykiety dla calej trasy mozna wygenerowac dopiero po spakowaniu wszystkich toreb.");
        }

        foreach (var bag in route.Bags.Where(b => !b.HasLabels))
        {
            await GenerateTransportLabelsForBagAsync(bag.PackingBagId);
        }

        return (await GetTransportLabelsForRouteAsync(date, routeId)).ToList();
    }

    public async Task<PackingLabelDto> ConfirmTransportLabelAttachedAsync(int labelId)
    {
        var label = await labelRepository.GetByIdAsync(labelId)
            ?? throw new InvalidOperationException($"Etykieta transportowa #{labelId} nie istnieje.");
        if (label.LabelType != LabelType.Shipping)
        {
            throw new InvalidOperationException("Potwierdzic przyklejenie mozna tylko dla etykiety transportowej.");
        }

        var latestLabel = await GetLatestShippingLabelForLabelAsync(label);
        if (latestLabel?.Id != label.Id)
        {
            throw new InvalidOperationException("Potwierdzic przyklejenie mozna tylko dla najnowszej etykiety torby.");
        }

        if (!label.AttachedAt.HasValue)
        {
            label.AttachedAt = DateTimeOffset.UtcNow;
            label.AttachedByUserId = currentUserService.GetUserId();
            label.AttachedBy = currentUserService.GetUserName() ?? "Packing";
            await labelRepository.UpdateAsync(label);
        }

        return await CreateTransportLabelDtoAsync(label);
    }

    public async Task<int> ConfirmTransportLabelsAttachedAsync(IReadOnlyCollection<int> labelIds)
    {
        var ids = labelIds.Where(id => id > 0).Distinct().ToArray();
        var count = 0;
        foreach (var labelId in ids)
        {
            await ConfirmTransportLabelAttachedAsync(labelId);
            count++;
        }

        return count;
    }

    public async Task<IEnumerable<PackingSessionDto>> GetSessionsByDateAsync(DateOnly date)
    {
        var sessions = await sessionRepository.GetByDateWithItemsAsync(date);
        return sessions.Select(MapSession).ToList();
    }

    public async Task<PackingSessionDto?> GetSessionByIdAsync(int sessionId)
    {
        var session = await sessionRepository.GetWithItemsAsync(sessionId);
        return session is null ? null : MapSession(session);
    }

    public async Task<FoilLabelPreparationResultDto> EnsureFoilBoxesForDateAsync(DateOnly date)
    {
        var result = new FoilLabelPreparationResultDto();
        var sessions = (await sessionRepository.GetByDateWithItemsAsync(date))
            .Where(session => session.OrderId.HasValue)
            .ToList();

        foreach (var session in sessions.Where(session => session.Items.Count == 0))
        {
            try
            {
                result.CreatedBoxes += (await PrepareOrderBoxesAsync(session.Id)).Count();
            }
            catch (InvalidOperationException ex)
            {
                result.SkippedSessions++;
                result.Errors.Add($"Torba #{session.Id}, zamowienie #{session.OrderId}: {ex.Message}");
                logger.LogWarning(
                    ex,
                    "Nie mozna przygotowac pudelek do foliowania dla torby {SessionId} zamowienia {OrderId}.",
                    session.Id,
                    session.OrderId);
            }
        }

        return result;
    }

    public async Task<FoilLabelDashboardDto> GetFoilLabelDashboardAsync(FoilLabelFilterDto filter)
    {
        var normalized = NormalizeFoilLabelFilter(filter);
        var query = new PackingItemQuery
        {
            PackingDate = normalized.Date,
            Search = normalized.Search,
            Status = ParsePackingItemStatus(normalized.Status),
            LabelState = normalized.LabelState,
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            SortBy = normalized.SortBy,
            SortDescending = string.Equals(normalized.SortDirection, "desc", StringComparison.OrdinalIgnoreCase),
        };

        var (rows, totalCount) = await sessionRepository.SearchPackingItemsAsync(query);
        var summary = await sessionRepository.GetFoilLabelSummaryAsync(normalized.Date);

        return new FoilLabelDashboardDto
        {
            Filter = normalized,
            Items = new KuchniaUCygana.Application.DTOs.Warehouse.PagedResultDto<FoilLabelItemDto>
            {
                Items = rows.Select(MapFoilLabelItem).ToList(),
                Page = normalized.Page,
                PageSize = normalized.PageSize,
                TotalCount = totalCount,
            },
            TotalBoxes = summary.TotalBoxes,
            PendingCount = summary.PendingCount,
            PrintedCount = summary.PrintedCount,
            ReprintCount = summary.ReprintCount,
            BlockedCount = summary.BlockedCount,
            PackedCount = summary.PackedCount,
        };
    }

    public async Task<PackingLabelDto> PrintFoilLabelAsync(
        int packingItemId,
        string operatorName,
        string? reprintReason = null)
    {
        var item = await itemRepository.GetByIdAsync(packingItemId)
            ?? throw new InvalidOperationException($"Pudełko #{packingItemId} nie istnieje.");

        var printNumber = await boxLabelRepository.GetPrintCountAsync(packingItemId) + 1;
        if (printNumber > 1 && string.IsNullOrWhiteSpace(reprintReason))
        {
            throw new InvalidOperationException("Powód redruku etykiety produktowej jest wymagany.");
        }

        if (item.Status is PackingItemStatus.Damaged or PackingItemStatus.Missing)
        {
            throw new InvalidOperationException("Nie można drukować etykiety produktowej dla pudełka oznaczonego jako uszkodzone albo brakujące.");
        }

        await EnsureProductionItemReadyForFoilAsync(item);

        var ingredients = (await sessionRepository.GetMealIngredientsAsync(item.MealId)).ToList();
        var allergens = (await sessionRepository.GetMealAllergensAsync(item.MealId)).ToList();
        var calories = await sessionRepository.GetMealCaloriesAsync(item.MealId);
        var qrCode = item.BoxCode ?? $"BOX-{item.Id:D6}";
        var printedAt = DateTimeOffset.UtcNow;
        var label = new BoxLabel
        {
            PackingItemId = packingItemId,
            QrCode = qrCode,
            PrintNumber = printNumber,
            ReprintReason = printNumber > 1 ? reprintReason!.Trim() : null,
            PrintedAt = printedAt,
            PrintedBy = string.IsNullOrWhiteSpace(operatorName) ? "Kuchnia" : operatorName,
            LabelDataJson = JsonSerializer.Serialize(
                new
                {
                    qrCode,
                    item.Id,
                    item.PackingSessionId,
                    item.PackingBagId,
                    item.MealId,
                    item.MealName,
                    item.DietVariantId,
                    ingredients,
                    allergens,
                    kcal = calories,
                    printNumber,
                    printedAt,
                    printedBy = string.IsNullOrWhiteSpace(operatorName) ? "Kuchnia" : operatorName,
                }),
        };

        var labelId = await boxLabelRepository.InsertAsync(label);
        label.Id = labelId;

        if (item.Status == PackingItemStatus.Pending)
        {
            item.Status = PackingItemStatus.FoilPrinted;
            item.FoilPrintedAt = DateTimeOffset.UtcNow;
            item.PackedBy = operatorName;
            await itemRepository.UpdateAsync(item);
        }

        return new PackingLabelDto
        {
            Id = label.Id,
            PackingItemId = label.PackingItemId,
            PackingSessionId = item.PackingSessionId,
            PackingBagId = item.PackingBagId,
            LabelType = LabelType.Product.ToString(),
            QrCode = label.QrCode,
            DishName = item.MealName,
            Allergens = allergens.Any() ? string.Join(", ", allergens) : "Brak",
            Kcal = calories,
            Ingredients = ingredients.Any() ? string.Join(", ", ingredients) : "Brak danych",
            ReprintReason = label.ReprintReason,
            PrintNumber = label.PrintNumber,
            PrintedAt = label.PrintedAt,
            PrintedBy = label.PrintedBy,
            LabelDataJson = label.LabelDataJson,
        };
    }

    public async Task<PackingLabelDto?> GetLatestFoilLabelAsync(int packingItemId)
    {
        var item = await itemRepository.GetByIdAsync(packingItemId)
            ?? throw new InvalidOperationException($"Pudełko #{packingItemId} nie istnieje.");
        var label = await boxLabelRepository.GetLatestForPackingItemAsync(packingItemId);
        if (label is null)
        {
            return null;
        }

        var ingredients = (await sessionRepository.GetMealIngredientsAsync(item.MealId)).ToList();
        var allergens = (await sessionRepository.GetMealAllergensAsync(item.MealId)).ToList();
        var calories = await sessionRepository.GetMealCaloriesAsync(item.MealId);

        return new PackingLabelDto
        {
            Id = label.Id,
            PackingItemId = label.PackingItemId,
            PackingSessionId = item.PackingSessionId,
            PackingBagId = item.PackingBagId,
            LabelType = LabelType.Product.ToString(),
            QrCode = label.QrCode,
            DishName = item.MealName,
            Allergens = allergens.Any() ? string.Join(", ", allergens) : "Brak",
            Kcal = calories,
            Ingredients = ingredients.Any() ? string.Join(", ", ingredients) : "Brak danych",
            ReprintReason = label.ReprintReason,
            PrintNumber = label.PrintNumber,
            PrintedAt = label.PrintedAt,
            PrintedBy = label.PrintedBy,
            LabelDataJson = label.LabelDataJson,
        };
    }

    public async Task PackBoxByCodeAsync(int sessionId, string barcode, string packedBy)
    {
        var result = await ScanBoxAsync(sessionId, barcode, packedBy);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    private PackingSessionDto MapSession(PackingSession session)
    {
        var dto = mapper.Map<PackingSessionDto>(session);
        var activeItems = GetActiveItems(session).ToList();
        dto.ClientPublicIdDisplay = GetClientPublicIdDisplay(session);
        dto.Items = session.Items
            .OrderBy(i => i.Id)
            .Select(item => MapPackingItemForSession(item, session))
            .ToList();
        dto.CanScan = session.Status == PackingStatus.Pending && activeItems.Count > 0;
        dto.CanCloseBag = session.Status == PackingStatus.Pending
            && activeItems.Count > 0
            && activeItems.All(i => i.Status == PackingItemStatus.Packed);
        dto.BlockReason = GetSessionBlockReason(session, activeItems);
        return dto;
    }

    private async Task<ScanBoxResponse> PackBoxInternalAsync(
        PackingSession session,
        PackingItem item,
        string packedBy)
    {
        var sessionItem = session.Items.FirstOrDefault(i => i.Id == item.Id) ?? item;
        if (sessionItem.PackingSessionId != session.Id)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko należy do innej dostawy.");
        }

        if (session.Status != PackingStatus.Pending)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Torba jest już zamknięta. Skanowanie pudełek jest zablokowane.");
        }

        var physicalBag = sessionItem.PackingBagId.HasValue
            ? await bagRepository.GetByIdAsync(sessionItem.PackingBagId.Value)
            : await EnsureDefaultBagAsync(session.Id);

        if (physicalBag is null)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko nie jest przypisane do aktywnej torby.");
        }

        if (physicalBag.Status is PackingBagStatus.Packed
            or PackingBagStatus.Labeled
            or PackingBagStatus.Manifested
            or PackingBagStatus.Loaded
            or PackingBagStatus.Dispatched)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Torba jest już zamknięta albo w procesie załadunku.");
        }

        if (physicalBag.Status == PackingBagStatus.Damaged)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Torba jest oznaczona jako uszkodzona. Użyj nowej torby.");
        }

        if (await boxLabelRepository.GetLatestForPackingItemAsync(sessionItem.Id) is null)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko nie ma ważnej etykiety produktowej z kuchni.");
        }

        if (sessionItem.Status == PackingItemStatus.Pending)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko nie jest gotowe do pakowania. Najpierw kuchnia musi wydrukować etykietę produktową.");
        }

        if (sessionItem.Status == PackingItemStatus.Damaged)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko jest oznaczone jako uszkodzone i wymaga zamiennika.");
        }

        if (sessionItem.Status == PackingItemStatus.Missing)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko jest oznaczone jako brakujące i wymaga zamiennika.");
        }

        if (sessionItem.Status == PackingItemStatus.Packed)
        {
            return CreateScanBoxResponse(session, sessionItem, false, "Pudełko jest już spakowane.");
        }

        if (sessionItem.Status != PackingItemStatus.FoilPrinted)
        {
            return CreateScanBoxResponse(session, sessionItem, false, $"Nie można spakować pudełka w statusie {sessionItem.Status}.");
        }

        sessionItem.Status = PackingItemStatus.Packed;
        sessionItem.PackedAt = DateTimeOffset.UtcNow;
        sessionItem.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? "Packing" : packedBy;
        await itemRepository.UpdateAsync(sessionItem);

        if (string.IsNullOrWhiteSpace(session.PackedBy) && !string.IsNullOrWhiteSpace(packedBy))
        {
            session.PackedBy = packedBy;
            await sessionRepository.UpdateAsync(session);
        }

        return CreateScanBoxResponse(session, sessionItem, true, $"Spakowano pudełko {sessionItem.BoxCode}.");
    }

    private PackingItemDto MapPackingItemForSession(PackingItem item, PackingSession session)
    {
        var dto = mapper.Map<PackingItemDto>(item);
        dto.CanPackManually = session.Status == PackingStatus.Pending
            && item.Status == PackingItemStatus.FoilPrinted;
        dto.CanReportIssue = session.Status == PackingStatus.Pending
            && item.Status is PackingItemStatus.Pending or PackingItemStatus.FoilPrinted or PackingItemStatus.Packed;
        dto.BlockReason = GetPackingItemBlockReason(item, session);
        return dto;
    }

    private static IEnumerable<PackingItem> GetActiveItems(PackingSession session)
    {
        return session.Items
            .Where(item => item.Status is not PackingItemStatus.Damaged and not PackingItemStatus.Missing);
    }

    private static void EnsureSessionCanBeEdited(PackingSession session)
    {
        if (session.Status != PackingStatus.Pending)
        {
            throw new InvalidOperationException("Zmiana pudełek jest dostępna tylko przed zamknięciem torby.");
        }
    }

    private static ScanBoxResponse CreateScanBoxResponse(
        PackingSession session,
        PackingItem? item,
        bool success,
        string message)
    {
        return new ScanBoxResponse
        {
            Success = success,
            Message = message,
            PackingSessionId = session.Id,
            PackingItemId = item?.Id ?? 0,
            MealName = item?.MealName ?? string.Empty,
            BoxCode = item?.BoxCode ?? string.Empty,
            AllBoxesPacked = GetActiveItems(session).Any()
                && GetActiveItems(session).All(i => i.Status == PackingItemStatus.Packed),
            ClientName = string.Empty,
            ClientPublicId = GetClientPublicIdDisplay(session),
            Status = item?.Status.ToString() ?? string.Empty,
        };
    }

    private static string GetClientPublicIdDisplay(PackingSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ClientPublicId))
        {
            return session.ClientPublicId;
        }

        if (session.DeliveryCalendarId.HasValue)
        {
            return $"DC-{session.DeliveryCalendarId.Value}";
        }

        return session.OrderId.HasValue ? $"ORD-{session.OrderId.Value}" : $"SESSION-{session.Id}";
    }

    private static string GetClientPublicIdDisplay(PackingItemSearchRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.ClientPublicId))
        {
            return row.ClientPublicId;
        }

        if (row.DeliveryCalendarId.HasValue)
        {
            return $"DC-{row.DeliveryCalendarId.Value}";
        }

        return row.OrderId.HasValue ? $"ORD-{row.OrderId.Value}" : $"SESSION-{row.PackingSessionId}";
    }

    private static FoilLabelFilterDto NormalizeFoilLabelFilter(FoilLabelFilterDto filter)
        => new()
        {
            Date = filter.Date == default ? DateOnly.FromDateTime(DateTime.Today) : filter.Date,
            Search = NormalizeFoilSearch(filter.Search),
            Status = ParsePackingItemStatus(filter.Status)?.ToString(),
            LabelState = NormalizeLabelState(filter.LabelState),
            SortBy = NormalizeFoilSortBy(filter.SortBy),
            SortDirection = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc",
            Page = Math.Max(filter.Page, 1),
            PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 10, 100),
        };

    private static string? NormalizeFoilSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static PackingItemStatus? ParsePackingItemStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<PackingItemStatus>(status.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeLabelState(string? labelState)
    {
        if (string.IsNullOrWhiteSpace(labelState) || labelState.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return labelState.Trim().ToLowerInvariant() switch
        {
            "missing" or "pending" => "missing",
            "printed" or "done" => "printed",
            "reprint" or "reprinted" => "reprint",
            "blocked" or "problem" => "blocked",
            _ => null,
        };
    }

    private static string NormalizeFoilSortBy(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "meal" or "mealname" => "meal",
            "box" or "boxcode" => "box",
            "status" => "status",
            "client" => "client",
            "order" => "order",
            "printed" => "printed",
            "route" => "route",
            _ => "id",
        };
    }

    private static FoilLabelItemDto MapFoilLabelItem(PackingItemSearchRow row)
        => new()
        {
            Id = row.Id,
            PackingSessionId = row.PackingSessionId,
            PackingBagId = row.PackingBagId,
            ProductionPlanItemId = row.ProductionPlanItemId,
            MealId = row.MealId,
            MealName = row.MealName,
            DietVariantId = row.DietVariantId,
            BoxCode = string.IsNullOrWhiteSpace(row.BoxCode) ? $"BOX-{row.Id:D6}" : row.BoxCode,
            Status = row.Status.ToString(),
            FoilPrintedAt = row.FoilPrintedAt,
            PackedAt = row.PackedAt,
            IsDamaged = row.IsDamaged,
            Remarks = row.Remarks,
            OrderId = row.OrderId,
            DeliveryCalendarId = row.DeliveryCalendarId,
            ClientName = row.ClientName,
            ClientPublicIdDisplay = GetClientPublicIdDisplay(row),
            RouteId = row.RouteId,
            StopNumber = row.StopNumber,
            ProductLabelPrintCount = row.ProductLabelPrintCount,
            LatestProductLabelPrintedAt = row.LatestProductLabelPrintedAt,
            ProductionStatus = row.ProductionStatus?.ToString(),
            PackagingDeductedAt = row.PackagingDeductedAt,
            FoilBlockReason = GetFoilBlockReason(row),
        };

    private async Task EnsureProductionItemReadyForFoilAsync(PackingItem item)
    {
        if (!item.ProductionPlanItemId.HasValue)
        {
            throw new InvalidOperationException("Nie mozna wydrukowac etykiety produktowej: pudelko nie jest powiazane z pozycja planu produkcji.");
        }

        var planItem = await productionPlanItemRepository.GetByIdAsync(item.ProductionPlanItemId.Value);
        if (planItem is null || planItem.IsDeleted)
        {
            throw new InvalidOperationException("Nie mozna wydrukowac etykiety produktowej: pozycja planu produkcji nie istnieje.");
        }

        if (planItem.Status != ProductionItemStatus.Cooked)
        {
            throw new InvalidOperationException("Nie mozna wydrukowac etykiety produktowej: danie nie ma potwierdzonego ugotowania.");
        }

        if (!planItem.PackagingDeductedAt.HasValue)
        {
            throw new InvalidOperationException("Nie mozna wydrukowac etykiety produktowej: opakowania nie zostaly rozliczone po gotowaniu.");
        }
    }

    private static string? GetFoilBlockReason(PackingItemSearchRow row)
    {
        if (row.Status is PackingItemStatus.Damaged or PackingItemStatus.Missing || row.IsDamaged)
        {
            return string.IsNullOrWhiteSpace(row.Remarks)
                ? "Pudelko oznaczone jako uszkodzone albo brakujace."
                : row.Remarks;
        }

        if (!row.ProductionPlanItemId.HasValue)
        {
            return "Pudelko nie jest powiazane z pozycja planu produkcji.";
        }

        if (row.ProductionStatus != ProductionItemStatus.Cooked)
        {
            return "Danie nie ma potwierdzonego ugotowania.";
        }

        if (!row.PackagingDeductedAt.HasValue)
        {
            return "Opakowania nie zostaly rozliczone po gotowaniu.";
        }

        return null;
    }

    private static string? GetSessionBlockReason(PackingSession session, IReadOnlyCollection<PackingItem> activeItems)
    {
        if (session.Status is PackingStatus.Loaded or PackingStatus.Dispatched)
        {
            return "Torba jest już w załadunku albo wysyłce.";
        }

        if (session.Status is PackingStatus.Packed or PackingStatus.Labeled)
        {
            return "Torba została zamknięta. Zmiany wymagają procesu reklamacyjnego albo wymiany torby.";
        }

        if (activeItems.Count == 0)
        {
            return "Najpierw przygotuj pudełka dla dostawy.";
        }

        if (activeItems.Any(i => i.Status == PackingItemStatus.Pending))
        {
            return "Czekamy na etykiety produktowe z kuchni dla wszystkich aktywnych pudełek.";
        }

        if (activeItems.Any(i => i.Status == PackingItemStatus.FoilPrinted))
        {
            return "Zeskanuj albo spakuj ręcznie wszystkie aktywne pudełka.";
        }

        return null;
    }

    private static string? GetPackingItemBlockReason(PackingItem item, PackingSession session)
    {
        if (session.Status != PackingStatus.Pending)
        {
            return "Torba jest już zamknięta.";
        }

        return item.Status switch
        {
            PackingItemStatus.Pending => "Brak etykiety produktowej z kuchni.",
            PackingItemStatus.Packed => "Pudełko jest już spakowane.",
            PackingItemStatus.Damaged => string.IsNullOrWhiteSpace(item.Remarks)
                ? "Pudełko oznaczone jako uszkodzone."
                : item.Remarks,
            PackingItemStatus.Missing => string.IsNullOrWhiteSpace(item.Remarks)
                ? "Pudełko oznaczone jako brakujące."
                : item.Remarks,
            _ => null,
        };
    }

    private async Task<IEnumerable<BoxDefinition>> BuildBoxDefinitionsAsync(PackingSession session)
    {
        var orderId = session.OrderId!.Value;
        var productionItems = await GetProductionPlanItemsForDateAsync(session.PackingDate);
        var deliveries = await orderProvider.GetDeliveriesForDateAsync(session.PackingDate.ToDateTime(TimeOnly.MinValue));
        var delivery = deliveries.FirstOrDefault(d =>
            session.DeliveryCalendarId.HasValue
                ? d.DeliveryCalendarId == session.DeliveryCalendarId.Value
                : d.OrderId == orderId);

        if (delivery is not null && delivery.Items.Count > 0)
        {
            return await ResolveOrderItemBoxDefinitionsAsync(delivery.Items, session.PackingDate, productionItems);
        }

        var order = await orderProvider.GetOrderByIdAsync(orderId)
            ?? throw new InvalidOperationException($"Zamówienie {orderId} nie istnieje.");

        var dietPlan = (await dietProvider.GetPlanForDateAsync(session.PackingDate))
            .Where(p => p.DietVariantId == order.DietVariantId)
            .Take(5)
            .ToList();

        if (dietPlan.Count > 0)
        {
            return dietPlan.Select(p =>
            {
                var productionItem = FindProductionItem(productionItems, p.DietMenuPlanItemId, p.MealId, p.DietVariantId);
                return new BoxDefinition(
                    productionItem?.MealId ?? p.MealId,
                    productionItem?.MealName ?? p.MealName,
                    productionItem?.DietVariantId ?? p.DietVariantId,
                    productionItem?.Id);
            });
        }

        throw new InvalidOperationException(
            $"Brak pozycji dostawy lub planu M2 dla wariantu diety {order.DietVariantId} na {session.PackingDate:dd.MM.yyyy}. Nie można utworzyć pudełek z danych zastępczych.");
    }

    private async Task<IEnumerable<BoxDefinition>> ResolveOrderItemBoxDefinitionsAsync(
        IReadOnlyList<OrderItemInfo> items,
        DateOnly packingDate,
        IReadOnlyList<ProductionPlanItem> productionItems)
    {
        var dietPlan = (await dietProvider.GetPlanForDateAsync(packingDate)).ToList();
        return items.Select(item =>
        {
            if (item.MealId.HasValue && item.MealId.Value > 0)
            {
                var explicitPlanItem = item.DietMenuPlanItemId.HasValue
                    ? dietPlan.FirstOrDefault(planItem =>
                        planItem.DietMenuPlanItemId == item.DietMenuPlanItemId.Value)
                    : null;

                if (explicitPlanItem is null && item.MealVariantId.HasValue)
                {
                    explicitPlanItem = dietPlan.FirstOrDefault(planItem =>
                        planItem.MealId == item.MealId.Value &&
                        planItem.DietVariantId == item.DietVariantId &&
                        planItem.MealVariantId == item.MealVariantId.Value);
                }

                explicitPlanItem ??= dietPlan.FirstOrDefault(planItem =>
                    planItem.MealId == item.MealId.Value &&
                    planItem.DietVariantId == item.DietVariantId);

                var productionItem = FindProductionItem(
                    productionItems,
                    item.DietMenuPlanItemId,
                    item.MealId,
                    explicitPlanItem?.DietVariantId ?? item.DietVariantId);

                return new BoxDefinition(
                    productionItem?.MealId ?? item.MealId.Value,
                    productionItem?.MealName ?? explicitPlanItem?.MealName ?? ExtractMealNameFromOrderItem(item),
                    productionItem?.DietVariantId ?? explicitPlanItem?.DietVariantId ?? item.DietVariantId,
                    productionItem?.Id);
            }

            var mealName = ExtractMealNameFromOrderItem(item);
            var mealSlot = ExtractMealSlotFromOrderItem(item);
            var matchedPlanItem = dietPlan.FirstOrDefault(planItem =>
                    planItem.DietVariantId == item.DietVariantId &&
                    string.Equals(planItem.MealName, mealName, StringComparison.OrdinalIgnoreCase))
                ?? dietPlan.FirstOrDefault(planItem =>
                    planItem.DietVariantId == item.DietVariantId &&
                    string.Equals(planItem.MealSlot, mealSlot, StringComparison.OrdinalIgnoreCase));

            var matchedProductionItem = FindProductionItem(
                productionItems,
                matchedPlanItem?.DietMenuPlanItemId ?? item.DietMenuPlanItemId,
                matchedPlanItem?.MealId,
                matchedPlanItem?.DietVariantId ?? item.DietVariantId);

            return new BoxDefinition(
                matchedProductionItem?.MealId ?? matchedPlanItem?.MealId ?? item.DietVariantId,
                matchedProductionItem?.MealName ?? matchedPlanItem?.MealName ?? mealName,
                matchedProductionItem?.DietVariantId ?? matchedPlanItem?.DietVariantId ?? item.DietVariantId,
                matchedProductionItem?.Id);
        });
    }

    private async Task<IReadOnlyList<ProductionPlanItem>> GetProductionPlanItemsForDateAsync(DateOnly date)
    {
        var plan = await productionPlanRepository.GetByDateAsync(date);
        if (plan is null)
        {
            return Array.Empty<ProductionPlanItem>();
        }

        return (await productionPlanRepository.GetPlanItemsAsync(plan.Id)).ToList();
    }

    private static ProductionPlanItem? FindProductionItem(
        IReadOnlyList<ProductionPlanItem> productionItems,
        int? dietMenuPlanItemId,
        int? mealId,
        int dietVariantId)
    {
        if (dietMenuPlanItemId.HasValue)
        {
            var bySnapshotItem = productionItems.FirstOrDefault(item =>
                item.DietMenuPlanItemId == dietMenuPlanItemId.Value);
            if (bySnapshotItem is not null)
            {
                return bySnapshotItem;
            }
        }

        if (mealId.HasValue && mealId.Value > 0)
        {
            return productionItems.FirstOrDefault(item =>
                item.MealId == mealId.Value &&
                item.DietVariantId == dietVariantId);
        }

        return null;
    }

    private static string ExtractMealNameFromOrderItem(OrderItemInfo item)
    {
        var dietName = item.DietName.Trim();
        var separatorIndex = dietName.LastIndexOf(':');
        if (separatorIndex >= 0 && separatorIndex < dietName.Length - 1)
        {
            return dietName[(separatorIndex + 1)..].Trim();
        }

        return $"{item.DietName} {item.VariantName}".Trim();
    }

    private static string ExtractMealSlotFromOrderItem(OrderItemInfo item)
    {
        var parts = item.VariantName.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    private static List<RouteAssignment> AssignOrdersToRoutes(
        IReadOnlyList<ActiveOrderEntry> orders,
        IReadOnlyDictionary<int, OrderDeliveryInfo> deliveries,
        IReadOnlyList<RouteEntry> routes)
    {
        var stopsByDeliveryCalendarId = routes
            .SelectMany(route => route.Stops
                .OrderBy(stop => stop.SequenceNumber)
                .Select(stop => new RouteStop(route, stop)))
            .Where(stop => stop.Stop.DeliveryCalendarId > 0)
            .GroupBy(stop => stop.Stop.DeliveryCalendarId)
            .ToDictionary(g => g.Key, g => g.First());

        var assignments = new List<RouteAssignment>();
        var unassignedRoute = new RouteEntry
        {
            RouteId = 0,
            RouteName = "Bez trasy M4",
            VehicleId = 0,
            VehicleRegistration = "BRAK",
        };

        foreach (var order in orders)
        {
            var deliveryCalendarId = order.DeliveryCalendarId;
            deliveries.TryGetValue(deliveryCalendarId > 0 ? deliveryCalendarId : order.OrderId, out var delivery);
            if (deliveryCalendarId <= 0)
            {
                deliveryCalendarId = delivery?.DeliveryCalendarId ?? 0;
            }

            var routeStop = deliveryCalendarId > 0 && stopsByDeliveryCalendarId.TryGetValue(deliveryCalendarId, out var matchedStop)
                ? matchedStop
                : new RouteStop(
                    unassignedRoute,
                    new RouteStopEntry
                    {
                        StopId = deliveryCalendarId > 0 ? deliveryCalendarId : order.OrderId,
                        SequenceNumber = 0,
                        DeliveryCalendarId = deliveryCalendarId,
                        DeliveryWindowFrom = string.Empty,
                        DeliveryWindowTo = string.Empty,
                    });

            assignments.Add(new RouteAssignment(
                order,
                routeStop.Route,
                routeStop.Stop,
                deliveryCalendarId,
                delivery?.ClientPublicId ?? order.ClientPublicId,
                delivery?.AddressFullLine ?? CreateFallbackAddress(order.OrderId),
                delivery?.DeliveryWindowName ?? $"{routeStop.Stop.DeliveryWindowFrom}-{routeStop.Stop.DeliveryWindowTo}".Trim('-')));
        }

        return assignments;
    }

    private static PackingBagDto CreateBagDto(PackingSession session, PackingBag physicalBag, RouteAssignment assignment, PackingLabel? transportLabel)
    {
        var activeItems = GetActiveItems(session)
            .Where(item => item.PackingBagId == physicalBag.Id || (!item.PackingBagId.HasValue && physicalBag.BagNumber == 1))
            .ToList();
        var totalBoxes = activeItems.Count;
        var packedBoxes = activeItems.Count(i => i.Status == PackingItemStatus.Packed);
        var status = physicalBag.Status.ToString();

        return new PackingBagDto
        {
            PackingBagId = physicalBag.Id,
            PackingSessionId = session.Id,
            DeliveryCalendarId = session.DeliveryCalendarId ?? assignment.DeliveryCalendarId,
            BagNumber = physicalBag.BagNumber,
            BagCode = physicalBag.BagCode,
            OrderId = session.OrderId ?? assignment.Order.OrderId,
            ClientName = session.ClientName ?? assignment.Order.ClientName,
            ClientPublicId = session.ClientPublicId ?? assignment.ClientPublicId,
            DietType = GetDietType(assignment.Order.DietVariantId),
            Address = assignment.Address,
            RouteId = assignment.Route.RouteId,
            RouteName = assignment.Route.RouteName,
            VehicleId = assignment.Route.VehicleId,
            VehicleRegistration = assignment.Route.VehicleRegistration,
            StopNumber = assignment.Stop.SequenceNumber,
            DeliveryWindow = assignment.DeliveryWindow,
            Status = status,
            StatusText = GetStatusText(physicalBag.Status, totalBoxes, packedBoxes),
            StatusColor = GetStatusColor(physicalBag.Status),
            TotalBoxes = totalBoxes,
            PackedBoxes = packedBoxes,
            HasLabels = transportLabel is not null,
            TransportLabelId = transportLabel?.Id,
            TransportLabelPrintNumber = transportLabel?.PrintNumber ?? 0,
            IsTransportLabelAttached = transportLabel?.AttachedAt.HasValue == true,
            TransportLabelAttachedAt = transportLabel?.AttachedAt,
            TransportLabelAttachedBy = transportLabel?.AttachedBy,
            CanPackBag = totalBoxes > 0
                && packedBoxes == totalBoxes
                && session.Status == PackingStatus.Pending
                && physicalBag.Status == PackingBagStatus.Pending,
            CanLoad = physicalBag.Status is PackingBagStatus.Labeled or PackingBagStatus.Manifested,
            CanGenerateTransportLabel = assignment.Route.RouteId > 0 && physicalBag.Status is PackingBagStatus.Packed or PackingBagStatus.Labeled or PackingBagStatus.Manifested or PackingBagStatus.Loaded or PackingBagStatus.Dispatched,
            CanConfirmTransportLabelAttached = transportLabel is not null &&
                !transportLabel.AttachedAt.HasValue &&
                physicalBag.Status is PackingBagStatus.Labeled or PackingBagStatus.Packed,
        };
    }

    private async Task<PackingBag> EnsureDefaultBagAsync(int sessionId)
    {
        var existing = await bagRepository.GetDefaultForSessionAsync(sessionId);
        if (existing is not null)
        {
            return existing;
        }

        var bag = new PackingBag
        {
            PackingSessionId = sessionId,
            BagNumber = 1,
            BagCode = $"BAG-{sessionId:D6}-01",
        };

        var id = await bagRepository.InsertAsync(bag);
        bag.Id = id;
        return bag;
    }

    private async Task<PackingLabel?> GetLatestShippingLabelAsync(int sessionId, int? packingBagId)
    {
        if (packingBagId.HasValue && packingLabelRepository is not null)
        {
            return await packingLabelRepository.GetLatestShippingForBagAsync(packingBagId.Value);
        }

        var labels = await labelRepository.GetAllAsync();
        return labels
            .Where(label => label.LabelType == LabelType.Shipping)
            .Where(label => packingBagId.HasValue
                ? label.PackingBagId == packingBagId.Value
                : label.PackingSessionId == sessionId)
            .OrderByDescending(label => label.PrintNumber)
            .ThenByDescending(label => label.Id)
            .FirstOrDefault();
    }

    private async Task<PackingLabel?> GetLatestShippingLabelForLabelAsync(PackingLabel label)
    {
        if (label.PackingBagId.HasValue && packingLabelRepository is not null)
        {
            return await packingLabelRepository.GetLatestShippingForBagAsync(label.PackingBagId.Value);
        }

        var labels = await labelRepository.GetAllAsync();
        return labels
            .Where(candidate => candidate.LabelType == LabelType.Shipping)
            .Where(candidate => label.PackingBagId.HasValue
                ? candidate.PackingBagId == label.PackingBagId.Value
                : candidate.PackingSessionId == label.PackingSessionId)
            .OrderByDescending(candidate => candidate.PrintNumber)
            .ThenByDescending(candidate => candidate.Id)
            .FirstOrDefault();
    }

    private async Task<PackingLabelDto> CreateTransportLabelDtoAsync(PackingLabel label)
    {
        PackingBag? physicalBag = null;
        if (label.PackingBagId.HasValue)
        {
            physicalBag = await bagRepository.GetByIdAsync(label.PackingBagId.Value);
        }

        var sessionId = physicalBag?.PackingSessionId ?? label.PackingSessionId;
        if (!sessionId.HasValue)
        {
            throw new InvalidOperationException("Etykieta transportowa nie jest powiazana z torba ani sesja.");
        }

        physicalBag ??= await bagRepository.GetDefaultForSessionAsync(sessionId.Value)
            ?? throw new InvalidOperationException("Nie znaleziono fizycznej torby dla etykiety transportowej.");
        var session = await sessionRepository.GetWithItemsAsync(physicalBag.PackingSessionId)
            ?? throw new InvalidOperationException("Nie znaleziono sesji kompletacji dla etykiety transportowej.");
        var board = await GetPackingBoardAsync(session.PackingDate);
        var routeBag = board.Routes
            .SelectMany(route => route.Bags)
            .FirstOrDefault(bag => bag.PackingBagId == physicalBag.Id || bag.PackingSessionId == session.Id)
            ?? throw new InvalidOperationException("Nie znaleziono trasy dla etykiety transportowej.");

        await SynchronizeTransportLabelWithRouteAsync(label, physicalBag, routeBag, BuildMealsList(session));

        return CreateTransportLabelDto(label, physicalBag, routeBag);
    }

    private async Task<int> GetShippingLabelPrintCountAsync(int sessionId, int packingBagId)
    {
        if (packingLabelRepository is not null)
        {
            return await packingLabelRepository.GetShippingPrintCountAsync(sessionId, packingBagId);
        }

        var labels = await labelRepository.GetAllAsync();
        return labels.Count(label =>
            label.LabelType == LabelType.Shipping &&
            (label.PackingBagId == packingBagId || label.PackingSessionId == sessionId));
    }

    private async Task<IReadOnlyDictionary<int, PackingLabel>> GetLatestShippingLabelsByBagAsync(IEnumerable<int> packingBagIds)
    {
        var ids = packingBagIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, PackingLabel>();
        }

        if (packingLabelRepository is not null)
        {
            return await packingLabelRepository.GetLatestShippingForBagsAsync(ids);
        }

        var labels = await labelRepository.GetAllAsync();
        return labels
            .Where(label => label.LabelType == LabelType.Shipping && label.PackingBagId.HasValue && ids.Contains(label.PackingBagId.Value))
            .GroupBy(label => label.PackingBagId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(label => label.PrintNumber).ThenByDescending(label => label.Id).First());
    }

    private async Task<IReadOnlyDictionary<int, PackingManifest>> GetLatestManifestsByRouteAsync(DateOnly date, IEnumerable<int> routeIds)
    {
        var ids = routeIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, PackingManifest>();
        }

        if (packingManifestQueryRepository is not null)
        {
            return await packingManifestQueryRepository.GetLatestByRoutesAsync(date, ids);
        }

        var manifests = await packingManifestRepository.GetAllAsync();
        return manifests
            .Where(m => m.PackingDate == date && m.RouteId.HasValue && ids.Contains(m.RouteId.Value) && !m.IsSuperseded)
            .GroupBy(m => m.RouteId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.GeneratedAt).ThenByDescending(m => m.Id).First());
    }

    private string BuildTransportQrCode(string bagCode)
    {
        var baseUrl = applicationUrlProvider.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/delivery/verify/{Uri.EscapeDataString(bagCode)}";
    }

    private async Task SynchronizeTransportLabelWithRouteAsync(
        PackingLabel label,
        PackingBag physicalBag,
        PackingBagDto routeBag,
        string? mealsList)
    {
        var routeInfo = BuildRouteInfo(routeBag);
        var deliveryWindow = string.IsNullOrWhiteSpace(routeBag.DeliveryWindow)
            ? label.DeliveryWindow
            : routeBag.DeliveryWindow;
        var effectiveMealsList = string.IsNullOrWhiteSpace(mealsList)
            ? label.MealsList
            : mealsList;
        var changed = false;

        if (!string.Equals(label.RouteInfo, routeInfo, StringComparison.Ordinal))
        {
            label.RouteInfo = routeInfo;
            changed = true;
        }

        if (!string.Equals(label.DeliveryWindow, deliveryWindow, StringComparison.Ordinal))
        {
            label.DeliveryWindow = deliveryWindow;
            changed = true;
        }

        if (!string.Equals(label.MealsList, effectiveMealsList, StringComparison.Ordinal))
        {
            label.MealsList = effectiveMealsList;
            changed = true;
        }

        var snapshot = BuildTransportLabelSnapshot(label, physicalBag, routeBag);
        if (!string.Equals(label.LabelDataJson, snapshot, StringComparison.Ordinal))
        {
            label.LabelDataJson = snapshot;
            changed = true;
        }

        if (changed && label.Id > 0)
        {
            await labelRepository.UpdateAsync(label);
        }
    }

    private static string BuildRouteInfo(PackingBagDto routeBag)
    {
        return $"{routeBag.RouteName}, auto {routeBag.VehicleRegistration}, stop {routeBag.StopNumber}";
    }

    private static string BuildMealsList(PackingSession session)
    {
        return string.Join(
            Environment.NewLine,
            GetActiveItems(session)
                .OrderBy(i => i.Id)
                .Select((item, index) => $"{index + 1}. {item.MealName}")
                .Distinct());
    }

    private static PackingLabelDto CreateTransportLabelDto(PackingLabel label, PackingBag bag, PackingBagDto routeBag)
    {
        return new PackingLabelDto
        {
            Id = label.Id,
            PackingItemId = label.PackingItemId,
            PackingSessionId = label.PackingSessionId,
            PackingBagId = label.PackingBagId,
            LabelType = label.LabelType.ToString(),
            QrCode = label.QrCode,
            ClientName = null,
            ClientPublicId = routeBag.ClientPublicId,
            Address = routeBag.Address,
            BagCode = bag.BagCode,
            DeliveryCalendarId = routeBag.DeliveryCalendarId,
            StopNumber = routeBag.StopNumber,
            VehicleRegistration = routeBag.VehicleRegistration,
            TotalBoxes = routeBag.TotalBoxes,
            PackedBoxes = routeBag.PackedBoxes,
            RouteInfo = BuildRouteInfo(routeBag),
            DeliveryWindow = string.IsNullOrWhiteSpace(routeBag.DeliveryWindow) ? label.DeliveryWindow : routeBag.DeliveryWindow,
            MealsList = label.MealsList,
            ReprintReason = label.ReprintReason,
            PrintNumber = label.PrintNumber,
            PrintedAt = label.PrintedAt,
            PrintedBy = label.PrintedBy,
            IsAttached = label.AttachedAt.HasValue,
            AttachedAt = label.AttachedAt,
            AttachedBy = label.AttachedBy,
            LabelDataJson = label.LabelDataJson,
        };
    }

    private static string BuildTransportLabelSnapshot(PackingLabel label, PackingBag bag, PackingBagDto routeBag)
    {
        return JsonSerializer.Serialize(
            new
            {
                label.QrCode,
                label.PrintNumber,
                label.ReprintReason,
                label.PrintedAt,
                label.PrintedBy,
                bag.Id,
                bag.BagCode,
                bag.BagNumber,
                routeBag.PackingSessionId,
                routeBag.DeliveryCalendarId,
                routeBag.ClientPublicId,
                routeBag.OrderId,
                routeBag.Address,
                routeBag.RouteId,
                routeBag.RouteName,
                routeBag.VehicleRegistration,
                routeBag.StopNumber,
                routeBag.DeliveryWindow,
                boxes = new
                {
                    total = routeBag.TotalBoxes,
                    packed = routeBag.PackedBoxes,
                },
                label.MealsList,
            });
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

        await statusLogRepository.InsertAsync(new PackingStatusLog
        {
            PackingSessionId = packingSessionId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = currentUserService.GetUserId(),
            ChangedAt = DateTime.UtcNow,
            Notes = notes,
        });
    }

    private static PackingRouteDto GetRouteOrThrow(PackingBoardDto board, int routeId)
    {
        return board.Routes.FirstOrDefault(r => r.RouteId == routeId)
            ?? throw new InvalidOperationException($"Dostawa/trasa {routeId} nie istnieje dla dnia {board.PackingDate:dd.MM.yyyy}.");
    }

    private static string CreateBoxCode(PackingSession session, int sequence)
    {
        var orderPart = session.OrderId?.ToString("D6") ?? session.Id.ToString("D6");
        return $"BOX-{orderPart}-{sequence:D2}";
    }

    private static string CreateFallbackAddress(int orderId)
    {
        return $"Warszawa, ul. Przykładowa {orderId % 100 + 1}";
    }

    private static string GetDietType(int dietVariantId)
    {
        return dietVariantId switch
        {
            1 => "Standard 2000 kcal",
            2 => "Slim 1500 kcal",
            3 => "Sport 2500 kcal",
            4 => "Keto 1800 kcal",
            5 => "Vege 1800 kcal",
            _ => $"Dieta wariant {dietVariantId}",
        };
    }

    private static string GetStatusText(PackingStatus status, int totalBoxes, int packedBoxes)
    {
        return status switch
        {
            PackingStatus.Pending when totalBoxes == 0 => "Oczekuje na pudełka",
            PackingStatus.Pending => "Pudełka do kompletacji",
            PackingStatus.Labeled => "Etykieta transportowa",
            PackingStatus.Packed => "Torba spakowana",
            PackingStatus.Loaded => "Załadowana do auta",
            PackingStatus.Dispatched => "Wysłana",
            _ when totalBoxes > 0 => $"{packedBoxes}/{totalBoxes} pudełek",
            _ => status.ToString(),
        };
    }

    private static string GetStatusText(PackingBagStatus status, int totalBoxes, int packedBoxes)
    {
        return status switch
        {
            PackingBagStatus.Pending when totalBoxes == 0 => "Oczekuje na pudełka",
            PackingBagStatus.Pending => "Pudełka do kompletacji",
            PackingBagStatus.Packed => "Torba spakowana",
            PackingBagStatus.Labeled => "Etykieta transportowa",
            PackingBagStatus.Manifested => "W manifeście",
            PackingBagStatus.Loaded => "Załadowana do auta",
            PackingBagStatus.Dispatched => "Wysłana",
            PackingBagStatus.Damaged => "Uszkodzona",
            _ when totalBoxes > 0 => $"{packedBoxes}/{totalBoxes} pudełek",
            _ => status.ToString(),
        };
    }

    private static string GetStatusColor(PackingStatus status)
    {
        return status switch
        {
            PackingStatus.Pending => "secondary",
            PackingStatus.Labeled => "azure",
            PackingStatus.Packed => "success",
            PackingStatus.Loaded => "indigo",
            PackingStatus.Dispatched => "dark",
            _ => "secondary",
        };
    }

    private static string GetStatusColor(PackingBagStatus status)
    {
        return status switch
        {
            PackingBagStatus.Pending => "secondary",
            PackingBagStatus.Packed => "success",
            PackingBagStatus.Labeled => "azure",
            PackingBagStatus.Manifested => "teal",
            PackingBagStatus.Loaded => "indigo",
            PackingBagStatus.Dispatched => "dark",
            PackingBagStatus.Damaged => "danger",
            _ => "secondary",
        };
    }

    private sealed record BoxDefinition(int MealId, string MealName, int DietVariantId, int? ProductionPlanItemId);

    private sealed record RouteStop(RouteEntry Route, RouteStopEntry Stop);

    private sealed record RouteAssignment(
        ActiveOrderEntry Order,
        RouteEntry Route,
        RouteStopEntry Stop,
        int DeliveryCalendarId,
        string? ClientPublicId,
        string Address,
        string DeliveryWindow);
}
