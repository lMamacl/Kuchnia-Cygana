using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Mappings;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Domain.Interfaces.Production;
using Microsoft.Extensions.Logging.Abstractions;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class PackingServiceRouteAssignmentTests
{
    [Fact]
    public async Task GetPackingBoardAsync_AssignsBagsByDeliveryCalendarId_NotOrderPosition()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository);
        await CreateSessionWithItemAsync(date, sessionRepository, bagRepository, itemRepository, PackingItemStatus.Pending, deliveryCalendarId: 100, orderId: 1);
        await CreateSessionWithItemAsync(date, sessionRepository, bagRepository, itemRepository, PackingItemStatus.Pending, deliveryCalendarId: 200, orderId: 2);

        var board = await service.GetPackingBoardAsync(date);

        var bags = board.Routes.Single().Bags.OrderBy(b => b.StopNumber).ToList();
        bags.Should().HaveCount(2);
        bags[0].DeliveryCalendarId.Should().Be(200);
        bags[0].OrderId.Should().Be(2);
        bags[1].DeliveryCalendarId.Should().Be(100);
        bags[1].OrderId.Should().Be(1);
    }

    [Fact]
    public async Task GenerateTransportLabelsAsync_DoesNotStoreClientNameInTransportLabelSnapshot()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var labelRepository = new InMemoryRepository<PackingLabel>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository,
            labelRepository);
        var (session, bag, _) = await CreatePackedSessionAsync(date, sessionRepository, bagRepository, itemRepository);

        var labels = (await service.GenerateTransportLabelsAsync(session.Id)).ToList();

        labels.Should().ContainSingle();
        labels.Single().ClientName.Should().BeNull();
        var stored = (await labelRepository.GetAllAsync()).Single();
        stored.ClientName.Should().BeNull();
        stored.LabelDataJson.Should().NotContain("Klient 1");
        stored.LabelDataJson.Should().NotContain("ClientName");
        stored.PackingBagId.Should().Be(bag.Id);
    }

    [Fact]
    public async Task ScanBoxAsync_RejectsInvalidBoxStates()
    {
        var cases = new[]
        {
            (Status: PackingItemStatus.FoilPrinted, HasLabel: false),
            (Status: PackingItemStatus.Pending, HasLabel: true),
            (Status: PackingItemStatus.Damaged, HasLabel: true),
            (Status: PackingItemStatus.Missing, HasLabel: true),
            (Status: PackingItemStatus.Packed, HasLabel: true),
        };

        foreach (var testCase in cases)
        {
            var date = new DateOnly(2035, 6, 1);
            var sessionRepository = new InMemoryPackingSessionRepository();
            var bagRepository = new InMemoryPackingBagRepository();
            var itemRepository = new InMemoryRepository<PackingItem>();
            var boxLabelRepository = new InMemoryBoxLabelRepository();
            var service = CreateService(
                sessionRepository,
                bagRepository,
                new ReorderedRouteManifestProvider(),
                new CalendarAwareOrderProvider(date),
                itemRepository,
                boxLabelRepository: boxLabelRepository);
            var (session, _, item) = await CreateSessionWithItemAsync(
                date,
                sessionRepository,
                bagRepository,
                itemRepository,
                testCase.Status);

            if (testCase.HasLabel)
            {
                await boxLabelRepository.InsertAsync(new BoxLabel
                {
                    PackingItemId = item.Id,
                    QrCode = item.BoxCode!,
                    PrintNumber = 1,
                    PrintedAt = DateTimeOffset.UtcNow,
                });
            }

            var result = await service.ScanBoxAsync(session.Id, item.BoxCode!, "packer");

            result.Success.Should().BeFalse($"status {testCase.Status} with label={testCase.HasLabel} must be rejected");
            result.ClientName.Should().BeEmpty();
            item.Status.Should().Be(testCase.Status);
        }
    }

    [Fact]
    public async Task ReportPackingItemIssueAsync_CreatesReplacementAndBlocksClosing()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository);
        var (session, _, item) = await CreateSessionWithItemAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            PackingItemStatus.FoilPrinted);

        var replacement = await service.ReportPackingItemIssueAsync(new ReportPackingItemIssueRequest
        {
            PackingItemId = item.Id,
            IssueType = nameof(PackingItemStatus.Damaged),
            Reason = "Pekniete opakowanie",
        });

        item.Status.Should().Be(PackingItemStatus.Damaged);
        item.ReplacementForPackingItemId.Should().BeNull();
        replacement.Status.Should().Be(nameof(PackingItemStatus.Pending));
        replacement.ReplacementForPackingItemId.Should().Be(item.Id);
        var refreshed = await service.GetSessionByIdAsync(session.Id);
        refreshed!.CanCloseBag.Should().BeFalse();
        refreshed.Items.Should().Contain(i => i.ReplacementForPackingItemId == item.Id);
    }

    [Fact]
    public async Task ReportPackingBagDamageAsync_CreatesNewBagAndRequiresRescan()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository);
        var (session, damagedBag, item) = await CreateSessionWithItemAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            PackingItemStatus.Packed);

        var replacementBag = await service.ReportPackingBagDamageAsync(new ReportPackingBagDamageRequest
        {
            PackingBagId = damagedBag.Id,
            Reason = "Rozerwana raczka",
        });

        damagedBag.Status.Should().Be(PackingBagStatus.Damaged);
        damagedBag.ReplacementPackingBagId.Should().Be(replacementBag.PackingBagId);
        replacementBag.BagCode.Should().NotBe(damagedBag.BagCode);
        item.PackingBagId.Should().Be(replacementBag.PackingBagId);
        item.Status.Should().Be(PackingItemStatus.FoilPrinted);
    }

    [Fact]
    public async Task PrintFoilLabelAsync_ReprintRequiresReason()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var boxLabelRepository = new InMemoryBoxLabelRepository();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository,
            boxLabelRepository: boxLabelRepository);
        var (_, _, item) = await CreateSessionWithItemAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            PackingItemStatus.Pending);

        await service.PrintFoilLabelAsync(item.Id, "kitchen");
        var act = async () => await service.PrintFoilLabelAsync(item.Id, "kitchen");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Powód redruku*");
    }

    [Fact]
    public async Task GenerateMissingTransportLabelsForRouteAsync_RejectsRouteWithUnpackedBag()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository);
        await CreateSessionWithItemAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            PackingItemStatus.FoilPrinted);

        var act = async () => await service.GenerateMissingTransportLabelsForRouteAsync(date, 7);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*spakowaniu wszystkich toreb*");
    }

    [Fact]
    public async Task GenerateMissingTransportLabelsForRouteAsync_CreatesOnlyMissingLabels()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var labelRepository = new InMemoryRepository<PackingLabel>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository,
            labelRepository);
        var (sessionOne, bagOne, _) = await CreatePackedSessionAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            deliveryCalendarId: 100,
            orderId: 1);
        await CreatePackedSessionAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            deliveryCalendarId: 200,
            orderId: 2);
        await labelRepository.InsertAsync(new PackingLabel
        {
            PackingSessionId = sessionOne.Id,
            PackingBagId = bagOne.Id,
            LabelType = LabelType.Shipping,
            QrCode = "https://test.local/delivery/verify/existing",
            PrintNumber = 1,
            PrintedAt = DateTimeOffset.UtcNow,
        });

        var labels = await service.GenerateMissingTransportLabelsForRouteAsync(date, 7);

        labels.Should().HaveCount(2);
        var stored = (await labelRepository.GetAllAsync()).ToList();
        stored.Should().HaveCount(2);
        stored.Count(label => label.PackingBagId == bagOne.Id).Should().Be(1);
    }

    [Fact]
    public async Task ConfirmTransportLabelAttachedAsync_RejectsOlderPrintAndConfirmsLatest()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var labelRepository = new InMemoryRepository<PackingLabel>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository,
            labelRepository);
        var (session, bag, _) = await CreatePackedSessionAsync(date, sessionRepository, bagRepository, itemRepository);
        var oldLabelId = await labelRepository.InsertAsync(new PackingLabel
        {
            PackingSessionId = session.Id,
            PackingBagId = bag.Id,
            LabelType = LabelType.Shipping,
            QrCode = "https://test.local/delivery/verify/old",
            PrintNumber = 1,
        });
        var latestLabelId = await labelRepository.InsertAsync(new PackingLabel
        {
            PackingSessionId = session.Id,
            PackingBagId = bag.Id,
            LabelType = LabelType.Shipping,
            QrCode = "https://test.local/delivery/verify/latest",
            PrintNumber = 2,
        });

        var oldAct = async () => await service.ConfirmTransportLabelAttachedAsync(oldLabelId);
        await oldAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*najnowszej etykiety*");

        var latest = await service.ConfirmTransportLabelAttachedAsync(latestLabelId);

        latest.IsAttached.Should().BeTrue();
        var storedLatest = await labelRepository.GetByIdAsync(latestLabelId);
        storedLatest!.AttachedAt.Should().NotBeNull();
        storedLatest.AttachedByUserId.Should().Be(7);
        storedLatest.AttachedBy.Should().Be("test-user");
    }

    [Fact]
    public async Task GenerateTransportLabelsAsync_ReprintStartsAsNotAttached()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var labelRepository = new InMemoryRepository<PackingLabel>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository,
            labelRepository);
        var (session, bag, _) = await CreatePackedSessionAsync(date, sessionRepository, bagRepository, itemRepository);
        await labelRepository.InsertAsync(new PackingLabel
        {
            PackingSessionId = session.Id,
            PackingBagId = bag.Id,
            LabelType = LabelType.Shipping,
            QrCode = "https://test.local/delivery/verify/attached",
            PrintNumber = 1,
            AttachedAt = DateTimeOffset.UtcNow,
            AttachedBy = "packer",
        });

        var labels = (await service.GenerateTransportLabelsAsync(session.Id, "Druk uszkodzony", forceNewPrint: true)).ToList();

        labels.Should().ContainSingle();
        labels.Single().PrintNumber.Should().Be(2);
        labels.Single().IsAttached.Should().BeFalse();
        var latest = (await labelRepository.GetAllAsync()).OrderByDescending(label => label.PrintNumber).First();
        latest.AttachedAt.Should().BeNull();
    }

    [Fact]
    public async Task GenerateTransportLabelsAsync_RefreshesExistingLabelWithCurrentRoute()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var labelRepository = new InMemoryRepository<PackingLabel>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new CalendarAwareOrderProvider(date),
            itemRepository,
            labelRepository);
        var (session, bag, _) = await CreatePackedSessionAsync(date, sessionRepository, bagRepository, itemRepository);
        await labelRepository.InsertAsync(new PackingLabel
        {
            PackingSessionId = session.Id,
            PackingBagId = bag.Id,
            LabelType = LabelType.Shipping,
            QrCode = "https://test.local/delivery/verify/stale",
            RouteInfo = "Stara trasa",
            DeliveryWindow = "00:00-00:00",
            MealsList = "Stare danie",
            LabelDataJson = "{}",
            PrintNumber = 1,
            PrintedAt = DateTimeOffset.UtcNow,
        });

        var labels = (await service.GenerateTransportLabelsAsync(session.Id)).ToList();

        labels.Should().ContainSingle();
        labels.Single().RouteInfo.Should().Be("Trasa testowa, auto WA TEST, stop 2");
        labels.Single().DeliveryWindow.Should().Be("08:00-09:00");
        labels.Single().MealsList.Should().Be("1. Test meal");
        var stored = (await labelRepository.GetAllAsync()).Single();
        stored.RouteInfo.Should().Be("Trasa testowa, auto WA TEST, stop 2");
        stored.DeliveryWindow.Should().Be("08:00-09:00");
        stored.MealsList.Should().Be("1. Test meal");
        stored.LabelDataJson.Should().Contain("\"RouteName\":\"Trasa testowa\"");
    }

    [Fact]
    public async Task PrepareOrderBoxesAsync_UsesOrderedMealNameFromDeliveryItems()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var service = CreateService(
            sessionRepository,
            bagRepository,
            new ReorderedRouteManifestProvider(),
            new SingleMealOrderProvider(date),
            itemRepository,
            dietProvider: new SingleMealDietDataProvider(date));
        var session = new PackingSession
        {
            PackingDate = date,
            OrderId = 77,
            DeliveryCalendarId = 777,
            ClientName = "Klient demo",
            ClientPublicId = "DEMO777",
            Status = PackingStatus.Pending,
        };
        session.Id = await sessionRepository.InsertAsync(session);

        var boxes = (await service.PrepareOrderBoxesAsync(session.Id)).ToList();

        boxes.Should().ContainSingle();
        boxes.Single().MealId.Should().Be(9001);
        boxes.Single().MealName.Should().Be("Kurczak z ryzem");
        boxes.Single().DietVariantId.Should().Be(42);
        var stored = (await itemRepository.GetAllAsync()).Single();
        stored.PackingBagId.Should().NotBeNull();
        stored.MealId.Should().Be(9001);
        stored.MealName.Should().Be("Kurczak z ryzem");
    }

    [Fact]
    public async Task RefreshFromRoutesAsync_CreatesSessionsOnlyForRouteStops_AndDoesNotDuplicateOnRefresh()
    {
        var date = new DateOnly(2035, 6, 1);
        var sessionRepository = new InMemoryPackingSessionRepository();
        var bagRepository = new InMemoryPackingBagRepository();
        var itemRepository = new InMemoryRepository<PackingItem>();
        var manifestRepository = new InMemoryRepository<PackingManifest>();
        var packingService = new RouteRefreshPackingService(sessionRepository, itemRepository);
        var service = new PackingSynchronizationService(
            sessionRepository,
            bagRepository,
            new RouteRefreshOrderProvider(date),
            NullLogger<PackingSynchronizationService>.Instance,
            manifestRepository,
            new ReorderedRouteManifestProvider(),
            packingService);
        var manifestId = await manifestRepository.InsertAsync(new PackingManifest
        {
            PackingDate = date,
            ManifestNumber = "M-TEST-1",
            RouteId = 7,
            RouteName = "Trasa testowa",
            GeneratedAt = DateTimeOffset.UtcNow,
            PayloadJson = "{}",
        });

        var first = await service.RefreshFromRoutesAsync(date, "tester");
        var second = await service.RefreshFromRoutesAsync(date, "tester");

        first.CreatedSessions.Should().Be(2);
        first.CreatedBags.Should().Be(2);
        first.CreatedItems.Should().Be(2);
        first.RefreshedRoutes.Should().Be(1);
        first.SupersededManifests.Should().Be(1);
        second.CreatedSessions.Should().Be(0);
        second.CreatedBags.Should().Be(0);
        second.CreatedItems.Should().Be(0);
        var sessions = (await sessionRepository.GetByDateWithItemsAsync(date)).ToList();
        sessions.Select(s => s.DeliveryCalendarId).Should().BeEquivalentTo(new int?[] { 100, 200 });
        sessions.Should().NotContain(s => s.DeliveryCalendarId == 300);
        (await bagRepository.GetAllAsync()).Should().HaveCount(2);
        (await itemRepository.GetAllAsync()).Should().HaveCount(2);
        var manifest = await manifestRepository.GetByIdAsync(manifestId);
        manifest!.RequiresRegeneration.Should().BeTrue();
    }

    private static async Task<(PackingSession Session, PackingBag Bag, PackingItem Item)> CreatePackedSessionAsync(
        DateOnly date,
        InMemoryPackingSessionRepository sessionRepository,
        InMemoryPackingBagRepository bagRepository,
        InMemoryRepository<PackingItem> itemRepository,
        int deliveryCalendarId = 100,
        int orderId = 1)
    {
        return await CreateSessionWithItemAsync(
            date,
            sessionRepository,
            bagRepository,
            itemRepository,
            PackingItemStatus.Packed,
            PackingStatus.Packed,
            PackingBagStatus.Packed,
            deliveryCalendarId,
            orderId);
    }

    private static async Task<(PackingSession Session, PackingBag Bag, PackingItem Item)> CreateSessionWithItemAsync(
        DateOnly date,
        InMemoryPackingSessionRepository sessionRepository,
        InMemoryPackingBagRepository bagRepository,
        InMemoryRepository<PackingItem> itemRepository,
        PackingItemStatus itemStatus,
        PackingStatus sessionStatus = PackingStatus.Pending,
        PackingBagStatus bagStatus = PackingBagStatus.Pending,
        int deliveryCalendarId = 100,
        int orderId = 1)
    {
        var session = new PackingSession
        {
            PackingDate = date,
            OrderId = orderId,
            DeliveryCalendarId = deliveryCalendarId,
            ClientName = $"Klient {orderId}",
            ClientPublicId = $"TEST{orderId:D4}",
            Status = sessionStatus,
        };
        session.Id = await sessionRepository.InsertAsync(session);

        var bag = new PackingBag
        {
            PackingSessionId = session.Id,
            BagNumber = 1,
            BagCode = $"BAG-{session.Id:D6}-01",
            Status = bagStatus,
        };
        bag.Id = await bagRepository.InsertAsync(bag);

        var item = new PackingItem
        {
            PackingSessionId = session.Id,
            PackingBagId = bag.Id,
            MealId = 501,
            MealName = "Test meal",
            DietVariantId = 1,
            BoxCode = $"BOX-{session.Id:D6}-01",
            Status = itemStatus,
            PackedAt = itemStatus == PackingItemStatus.Packed ? DateTimeOffset.UtcNow : null,
            PackedBy = itemStatus == PackingItemStatus.Packed ? "packer" : null,
        };
        item.Id = await itemRepository.InsertAsync(item);
        session.Items.Add(item);

        return (session, bag, item);
    }

    private static PackingService CreateService(
        IPackingSessionRepository sessionRepository,
        IPackingBagRepository bagRepository,
        IDeliveryManifestProvider manifestProvider,
        IOrderDataProvider orderProvider,
        IRepository<PackingItem>? itemRepository = null,
        IRepository<PackingLabel>? labelRepository = null,
        IRepository<PackingManifest>? manifestRepository = null,
        IBoxLabelRepository? boxLabelRepository = null,
        IDietDataProvider? dietProvider = null)
    {
        var mapperConfiguration = new MapperConfiguration(
            cfg => cfg.AddProfile<ProductionProfile>(),
            NullLoggerFactory.Instance);

        return new PackingService(
            sessionRepository,
            itemRepository ?? new InMemoryRepository<PackingItem>(),
            labelRepository ?? new InMemoryRepository<PackingLabel>(),
            manifestRepository ?? new InMemoryRepository<PackingManifest>(),
            bagRepository,
            boxLabelRepository ?? new InMemoryBoxLabelRepository(),
            new InMemoryPackingStatusLogRepository(),
            orderProvider,
            dietProvider ?? new EmptyDietDataProvider(),
            manifestProvider,
            new TestApplicationUrlProvider(),
            new TestCurrentUserService(),
            mapperConfiguration.CreateMapper(),
            NullLogger<PackingService>.Instance);
    }

    private sealed class CalendarAwareOrderProvider : IOrderDataProvider
    {
        private readonly DateOnly _date;

        public CalendarAwareOrderProvider(DateOnly date)
        {
            _date = date;
        }

        public Task<IEnumerable<ActiveOrderEntry>> GetActiveOrdersAsync(DateOnly deliveryDate)
        {
            return Task.FromResult<IEnumerable<ActiveOrderEntry>>(new[]
            {
                new ActiveOrderEntry
                {
                    DeliveryCalendarId = 100,
                    OrderId = 1,
                    ClientId = 1,
                    ClientPublicId = "TEST0001",
                    ClientName = "Klient 1",
                    DietVariantId = 1,
                    DeliveryDate = deliveryDate,
                },
                new ActiveOrderEntry
                {
                    DeliveryCalendarId = 200,
                    OrderId = 2,
                    ClientId = 2,
                    ClientPublicId = "TEST0002",
                    ClientName = "Klient 2",
                    DietVariantId = 2,
                    DeliveryDate = deliveryDate,
                },
            });
        }

        public Task<ActiveOrderEntry?> GetOrderByIdAsync(int orderId)
        {
            return Task.FromResult<ActiveOrderEntry?>(null);
        }

        public Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
        {
            return Task.FromResult<IEnumerable<OrderDeliveryInfo>>(new[]
            {
                CreateDelivery(100, 1, "Klient 1"),
                CreateDelivery(200, 2, "Klient 2"),
            });
        }

        private OrderDeliveryInfo CreateDelivery(int deliveryCalendarId, int orderId, string clientName)
        {
            return new OrderDeliveryInfo(
                deliveryCalendarId,
                orderId,
                $"ORD-{orderId}",
                orderId,
                $"TEST{orderId:D4}",
                clientName,
                $"Adres {orderId}",
                "Warszawa",
                "00-001",
                52.2,
                21.0,
                _date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(8))),
                "08:00-09:00",
                Array.Empty<OrderItemInfo>());
        }
    }

    private sealed class ReorderedRouteManifestProvider : IDeliveryManifestProvider
    {
        public Task<IEnumerable<RouteEntry>> GetRoutesForDateAsync(DateOnly date)
        {
            return Task.FromResult<IEnumerable<RouteEntry>>(new[]
            {
                new RouteEntry
                {
                    RouteId = 7,
                    RouteName = "Trasa testowa",
                    VehicleId = 1,
                    VehicleRegistration = "WA TEST",
                    Stops =
                    {
                        new RouteStopEntry { StopId = 1, SequenceNumber = 1, DeliveryCalendarId = 200 },
                        new RouteStopEntry { StopId = 2, SequenceNumber = 2, DeliveryCalendarId = 100 },
                    },
                },
            });
        }

        public Task<RouteEntry?> GetRouteByIdAsync(int routeId)
        {
            return Task.FromResult<RouteEntry?>(null);
        }
    }

    private sealed class SingleMealOrderProvider : IOrderDataProvider
    {
        private readonly DateOnly _date;

        public SingleMealOrderProvider(DateOnly date)
        {
            _date = date;
        }

        public Task<IEnumerable<ActiveOrderEntry>> GetActiveOrdersAsync(DateOnly deliveryDate)
        {
            return Task.FromResult(Enumerable.Empty<ActiveOrderEntry>());
        }

        public Task<ActiveOrderEntry?> GetOrderByIdAsync(int orderId)
        {
            return Task.FromResult<ActiveOrderEntry?>(null);
        }

        public Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
        {
            return Task.FromResult<IEnumerable<OrderDeliveryInfo>>(new[]
            {
                new OrderDeliveryInfo(
                    777,
                    77,
                    "DEMO-77",
                    77,
                    "DEMO777",
                    "Klient demo",
                    "Lipowa 1, 15-424 Bialystok",
                    "Bialystok",
                    "15-424",
                    53.132,
                    23.159,
                    _date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(8))),
                    "08:00-09:00",
                    new[]
                    {
                        new OrderItemInfo(
                            11,
                            "Dieta demo: Kurczak z ryzem",
                            42,
                            "1800 kcal / Lunch",
                            1800),
                    }),
            });
        }
    }

    private sealed class RouteRefreshOrderProvider : IOrderDataProvider
    {
        private readonly DateOnly _date;

        public RouteRefreshOrderProvider(DateOnly date)
        {
            _date = date;
        }

        public Task<IEnumerable<ActiveOrderEntry>> GetActiveOrdersAsync(DateOnly deliveryDate)
        {
            return Task.FromResult<IEnumerable<ActiveOrderEntry>>(new[]
            {
                CreateActive(100, 1, "Klient 1"),
                CreateActive(200, 2, "Klient 2"),
                CreateActive(300, 3, "Klient poza trasa"),
            });
        }

        public Task<ActiveOrderEntry?> GetOrderByIdAsync(int orderId)
        {
            return Task.FromResult<ActiveOrderEntry?>(null);
        }

        public Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
        {
            return Task.FromResult<IEnumerable<OrderDeliveryInfo>>(new[]
            {
                CreateDelivery(100, 1, "Klient 1"),
                CreateDelivery(200, 2, "Klient 2"),
                CreateDelivery(300, 3, "Klient poza trasa"),
            });
        }

        private ActiveOrderEntry CreateActive(int deliveryCalendarId, int orderId, string clientName)
        {
            return new ActiveOrderEntry
            {
                DeliveryCalendarId = deliveryCalendarId,
                OrderId = orderId,
                ClientId = orderId,
                ClientPublicId = $"TEST{orderId:D4}",
                ClientName = clientName,
                DietVariantId = orderId,
                DeliveryDate = _date,
            };
        }

        private OrderDeliveryInfo CreateDelivery(int deliveryCalendarId, int orderId, string clientName)
        {
            return new OrderDeliveryInfo(
                deliveryCalendarId,
                orderId,
                $"ORD-{orderId}",
                orderId,
                $"TEST{orderId:D4}",
                clientName,
                $"Adres {orderId}",
                "Bialystok",
                "15-001",
                53.132,
                23.159,
                _date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(8))),
                "08:00-09:00",
                Array.Empty<OrderItemInfo>());
        }
    }

    private sealed class RouteRefreshPackingService : IPackingService
    {
        private readonly InMemoryPackingSessionRepository sessionRepository;
        private readonly InMemoryRepository<PackingItem> itemRepository;

        public RouteRefreshPackingService(
            InMemoryPackingSessionRepository sessionRepository,
            InMemoryRepository<PackingItem> itemRepository)
        {
            this.sessionRepository = sessionRepository;
            this.itemRepository = itemRepository;
        }

        public async Task<IEnumerable<PackingItemDto>> PrepareOrderBoxesAsync(int packingSessionId)
        {
            var session = await this.sessionRepository.GetWithItemsAsync(packingSessionId)
                ?? throw new InvalidOperationException("Missing test session.");
            var item = new PackingItem
            {
                PackingSessionId = packingSessionId,
                MealId = 501,
                MealName = "Test meal",
                DietVariantId = 1,
                BoxCode = $"BOX-{packingSessionId:D6}-01",
                Status = PackingItemStatus.Pending,
            };
            item.Id = await this.itemRepository.InsertAsync(item);
            session.Items.Add(item);

            return new[]
            {
                new PackingItemDto
                {
                    Id = item.Id,
                    PackingSessionId = item.PackingSessionId,
                    MealId = item.MealId,
                    MealName = item.MealName,
                    DietVariantId = item.DietVariantId,
                    BoxCode = item.BoxCode,
                    Status = item.Status.ToString(),
                },
            };
        }

        public Task<PackingBoardDto> GetPackingBoardAsync(DateOnly date)
        {
            throw new NotSupportedException();
        }

        public Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy)
        {
            throw new NotSupportedException();
        }

        public Task MarkBoxPackedAsync(int packingItemId, string packedBy)
        {
            throw new NotSupportedException();
        }

        public Task PackOrderBagAsync(int packingSessionId, string packedBy)
        {
            throw new NotSupportedException();
        }

        public Task<ScanBoxResponse> ScanBoxAsync(int sessionId, string barcode, string packedBy)
        {
            throw new NotSupportedException();
        }

        public Task<PackingItemDto> ReportPackingItemIssueAsync(ReportPackingItemIssueRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<PackingBagDto> ReportPackingBagDamageAsync(ReportPackingBagDamageRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsAsync(
            int sessionId,
            string? reprintReason = null,
            bool forceNewPrint = false)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsForBagAsync(
            int packingBagId,
            string? reprintReason = null,
            bool forceNewPrint = false)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForSessionAsync(int sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForRouteAsync(DateOnly date, int routeId)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForDeliveryAsync(
            DateOnly date,
            int routeId,
            string? reprintReason = null,
            bool forceNewPrint = false)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PackingLabelDto>> GenerateMissingTransportLabelsForRouteAsync(DateOnly date, int routeId)
        {
            throw new NotSupportedException();
        }

        public Task<PackingLabelDto> ConfirmTransportLabelAttachedAsync(int labelId)
        {
            throw new NotSupportedException();
        }

        public Task<int> ConfirmTransportLabelsAttachedAsync(IReadOnlyCollection<int> labelIds)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<PackingSessionDto>> GetSessionsByDateAsync(DateOnly date)
        {
            throw new NotSupportedException();
        }

        public Task<PackingSessionDto?> GetSessionByIdAsync(int sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<FoilLabelPreparationResultDto> EnsureFoilBoxesForDateAsync(DateOnly date)
        {
            throw new NotSupportedException();
        }

        public Task<FoilLabelDashboardDto> GetFoilLabelDashboardAsync(FoilLabelFilterDto filter)
        {
            throw new NotSupportedException();
        }

        public Task PackBoxByCodeAsync(int sessionId, string barcode, string packedBy)
        {
            throw new NotSupportedException();
        }

        public Task<PackingLabelDto> PrintFoilLabelAsync(
            int packingItemId,
            string operatorName,
            string? reprintReason = null)
        {
            throw new NotSupportedException();
        }

        public Task<PackingLabelDto?> GetLatestFoilLabelAsync(int packingItemId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SingleMealDietDataProvider : EmptyDietDataProvider
    {
        private readonly DateOnly _date;

        public SingleMealDietDataProvider(DateOnly date)
        {
            _date = date;
        }

        public override Task<IEnumerable<DietPlanEntry>> GetPlanForDateAsync(DateOnly date)
        {
            return Task.FromResult<IEnumerable<DietPlanEntry>>(new[]
            {
                new DietPlanEntry
                {
                    PlanDate = _date,
                    MealId = 9001,
                    MealName = "Kurczak z ryzem",
                    DietVariantId = 42,
                    MealSlot = "Lunch",
                    SortOrder = 3,
                },
            });
        }
    }

    private class EmptyDietDataProvider : IDietDataProvider
    {
        public virtual Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate)
        {
            return Task.FromResult(Enumerable.Empty<DietPlanEntry>());
        }

        public virtual Task<IEnumerable<DietPlanEntry>> GetPlanForDateAsync(DateOnly date)
        {
            return Task.FromResult(Enumerable.Empty<DietPlanEntry>());
        }

        public virtual Task<PublishedDietPlanSnapshotDto?> GetPublishedPlanSnapshotAsync(DateOnly date)
        {
            return Task.FromResult<PublishedDietPlanSnapshotDto?>(null);
        }

        public virtual Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId)
        {
            return Task.FromResult(Enumerable.Empty<RecipeIngredientEntry>());
        }

        public virtual Task<MealCookingDetailsEntry?> GetMealCookingDetailsAsync(int mealId)
        {
            return Task.FromResult<MealCookingDetailsEntry?>(null);
        }
    }

    private sealed class TestApplicationUrlProvider : IApplicationUrlProvider
    {
        public string BaseUrl => "https://test.local";
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public int? GetUserId()
        {
            return 7;
        }

        public string? GetUserName()
        {
            return "test-user";
        }
    }

    private sealed class InMemoryPackingSessionRepository : InMemoryRepository<PackingSession>, IPackingSessionRepository
    {
        public Task<IEnumerable<PackingSession>> GetActiveByDateAsync(DateOnly date)
        {
            return Task.FromResult(this.Entities.Where(s => s.PackingDate == date));
        }

        public Task<PackingSession?> GetWithItemsAsync(int sessionId)
        {
            return Task.FromResult(this.Entities.FirstOrDefault(s => s.Id == sessionId));
        }

        public Task<PackingSession?> GetByDateAndOrderAsync(DateOnly date, int orderId)
        {
            return Task.FromResult(this.Entities.FirstOrDefault(s => s.PackingDate == date && s.OrderId == orderId));
        }

        public Task<IEnumerable<PackingSession>> GetByDateWithItemsAsync(DateOnly date)
        {
            return Task.FromResult(this.Entities.Where(s => s.PackingDate == date));
        }

        public Task<IEnumerable<PackingItem>> GetSessionItemsAsync(int sessionId)
        {
            return Task.FromResult(Enumerable.Empty<PackingItem>());
        }

        public Task<IEnumerable<PackingLabel>> GetLabelsBySessionAsync(int sessionId)
        {
            return Task.FromResult(Enumerable.Empty<PackingLabel>());
        }

        public Task<PackingLabel?> GetShippingLabelAsync(int sessionId)
        {
            return Task.FromResult<PackingLabel?>(null);
        }

        public Task<PackingLabel?> GetProductLabelAsync(int packingItemId)
        {
            return Task.FromResult<PackingLabel?>(null);
        }

        public Task<IEnumerable<string>> GetMealIngredientsAsync(int mealId)
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        public Task<IEnumerable<string>> GetMealAllergensAsync(int mealId)
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        public Task<int?> GetMealCaloriesAsync(int mealId)
        {
            return Task.FromResult<int?>(null);
        }

        public Task<(IReadOnlyList<PackingItemSearchRow> Items, int TotalCount)> SearchPackingItemsAsync(
            PackingItemQuery query)
        {
            return Task.FromResult<(IReadOnlyList<PackingItemSearchRow>, int)>(
                (Array.Empty<PackingItemSearchRow>(), 0));
        }

        public Task<FoilLabelSummary> GetFoilLabelSummaryAsync(DateOnly date)
        {
            return Task.FromResult(new FoilLabelSummary());
        }
    }

    private sealed class InMemoryPackingBagRepository : InMemoryRepository<PackingBag>, IPackingBagRepository
    {
        public Task<IEnumerable<PackingBag>> GetBySessionIdAsync(int packingSessionId)
        {
            return Task.FromResult(this.Entities.Where(b => b.PackingSessionId == packingSessionId));
        }

        public Task<IReadOnlyList<PackingBag>> GetBySessionIdsAsync(IEnumerable<int> packingSessionIds)
        {
            var ids = packingSessionIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<PackingBag>>(this.Entities.Where(b => ids.Contains(b.PackingSessionId)).ToList());
        }

        public Task<PackingBag?> GetByCodeAsync(string bagCode)
        {
            return Task.FromResult(this.Entities.FirstOrDefault(b => b.BagCode == bagCode));
        }

        public Task<PackingBag?> GetDefaultForSessionAsync(int packingSessionId)
        {
            return Task.FromResult(this.Entities
                .OrderBy(b => b.BagNumber)
                .FirstOrDefault(b => b.PackingSessionId == packingSessionId && b.Status != PackingBagStatus.Damaged));
        }
    }

    private sealed class InMemoryBoxLabelRepository : InMemoryRepository<BoxLabel>, IBoxLabelRepository
    {
        public Task<BoxLabel?> GetLatestForPackingItemAsync(int packingItemId)
        {
            return Task.FromResult(this.Entities.LastOrDefault(l => l.PackingItemId == packingItemId));
        }

        public Task<BoxLabel?> GetByQrCodeAsync(string qrCode)
        {
            return Task.FromResult(this.Entities.LastOrDefault(l => l.QrCode == qrCode));
        }

        public Task<int> GetPrintCountAsync(int packingItemId)
        {
            return Task.FromResult(this.Entities.Count(l => l.PackingItemId == packingItemId));
        }
    }

    private sealed class InMemoryPackingStatusLogRepository : InMemoryRepository<PackingStatusLog>, IPackingStatusLogRepository
    {
        public Task<IEnumerable<PackingStatusLog>> GetBySessionIdAsync(int packingSessionId)
        {
            return Task.FromResult(this.Entities.Where(l => l.PackingSessionId == packingSessionId));
        }
    }

    private class InMemoryRepository<TEntity> : IRepository<TEntity>
        where TEntity : BaseEntity<int>
    {
        private int _nextId = 1;

        protected List<TEntity> Entities { get; } = new();

        public Task<TEntity?> GetByIdAsync(int id)
        {
            return Task.FromResult(this.Entities.FirstOrDefault(e => e.Id == id));
        }

        public Task<IEnumerable<TEntity>> GetAllAsync()
        {
            return Task.FromResult<IEnumerable<TEntity>>(this.Entities);
        }

        public Task<int> InsertAsync(TEntity entity)
        {
            entity.Id = this._nextId++;
            this.Entities.Add(entity);
            return Task.FromResult(entity.Id);
        }

        public Task<bool> UpdateAsync(TEntity entity)
        {
            var index = this.Entities.FindIndex(e => e.Id == entity.Id);
            if (index >= 0)
            {
                this.Entities[index] = entity;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(int id)
        {
            return Task.FromResult(this.Entities.RemoveAll(e => e.Id == id) > 0);
        }
    }
}
