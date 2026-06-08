using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Orders;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_ShouldMaterializeCartItemsAgainstPublishedM2Plan()
    {
        var startDate = new DateTime(2026, 6, 8);
        var orderRepository = new Mock<IOrderRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var addressRepository = CreateAddressRepository(customerId: 44);
        var dietDataProvider = new Mock<IDietDataProvider>();
        var dietOrderingService = CreateDietOrderingService(serverPricePerDay: 99.99m);
        List<OrderItem> insertedItems = [];
        List<DeliveryCalendar> insertedDays = [];
        Order? insertedOrder = null;

        orderRepository
            .Setup(repo => repo.InsertCheckoutAsync(
                It.IsAny<Order>(),
                It.IsAny<IReadOnlyCollection<OrderItem>>(),
                It.IsAny<IReadOnlyCollection<DeliveryCalendar>>()))
            .Callback<Order, IReadOnlyCollection<OrderItem>, IReadOnlyCollection<DeliveryCalendar>>((order, items, days) =>
            {
                insertedOrder = order;
                insertedItems = items.ToList();
                insertedDays = days.ToList();
                foreach (var item in insertedItems) item.OrderId = 123;
                foreach (var day in insertedDays) day.OrderId = 123;
            })
            .ReturnsAsync(123);

        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 8)))
            .ReturnsAsync(CreateSnapshot(new DateOnly(2026, 6, 8), 1001, 1002));
        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 9)))
            .ReturnsAsync(CreateSnapshot(new DateOnly(2026, 6, 9), 2001, 2002));

        var service = CreateService(
            orderRepository,
            deliveryCalendarRepository,
            addressRepository,
            dietDataProvider,
            dietOrderingService);

        var orderId = await service.CreateOrderAsync(new CreateOrderRequest
        {
            AddressId = 7,
            DeliveryWindowId = 3,
            StartDate = startDate,
            Items =
            [
                new CreateOrderItemRequest
                {
                    DietId = 5,
                    DietVariantId = 10,
                    DietName = "Tampered Diet",
                    VariantName = "Tampered Variant",
                    CaloriesPerDay = 100,
                    PricePerDay = 1m,
                    TotalDays = 2,
                },
            ],
        }, customerId: 44);

        orderId.Should().Be(123);
        insertedItems.Should().HaveCount(4);
        insertedItems.Select(item => item.DietMenuPlanItemId).Should().BeEquivalentTo(new[] { 1001, 1002, 2001, 2002 });
        insertedItems.Select(item => item.MealId).Should().BeEquivalentTo(new[] { 801, 802, 801, 802 });
        insertedItems.Select(item => item.MealVariantId).Should().BeEquivalentTo(new int?[] { 901, null, 901, null });
        insertedItems.Select(item => item.MealSlot).Should().BeEquivalentTo(new[] { "Breakfast", "Dinner", "Breakfast", "Dinner" });
        insertedItems.Should().OnlyContain(item => item.TotalDays == 1);
        insertedItems.Should().OnlyContain(item => item.OrderId == 123);
        insertedItems.Should().OnlyContain(item =>
            item.DietName == "Dieta Sport" &&
            item.VariantName == "2200 kcal" &&
            item.CaloriesPerDay == 2200);
        insertedItems.Where(item => item.DeliveryDate == startDate.Date).Should().HaveCount(2);
        insertedItems.Where(item => item.DeliveryDate == startDate.Date.AddDays(1)).Should().HaveCount(2);
        insertedItems.GroupBy(item => item.DeliveryDate)
            .Should().OnlyContain(group => group.Sum(item => item.TotalPrice) == 99.99m);

        insertedDays.Should().HaveCount(2);
        insertedDays.Select(day => day.DeliveryDate).Should().Equal(startDate.Date, startDate.Date.AddDays(1));
        insertedDays.Should().OnlyContain(day => day.AddressId == 7 && day.DeliveryWindowId == 3);

        insertedOrder.Should().NotBeNull();
        insertedOrder!.TotalPrice.Should().Be(199.98m);
        insertedOrder.FinalPrice.Should().Be(199.98m);
        insertedOrder.StartDate.Should().Be(startDate.Date);
        insertedOrder.EndDate.Should().Be(startDate.Date.AddDays(1));
        orderRepository.Verify(repo => repo.InsertCheckoutAsync(
            It.IsAny<Order>(),
            It.IsAny<IReadOnlyCollection<OrderItem>>(),
            It.IsAny<IReadOnlyCollection<DeliveryCalendar>>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldRejectCheckoutWhenPublishedM2PlanIsMissing()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var addressRepository = CreateAddressRepository(customerId: 44);
        var dietDataProvider = new Mock<IDietDataProvider>();
        var dietOrderingService = CreateDietOrderingService(serverPricePerDay: 99.99m);

        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 8)))
            .ReturnsAsync((PublishedDietPlanSnapshotDto?)null);

        var service = CreateService(
            orderRepository,
            deliveryCalendarRepository,
            addressRepository,
            dietDataProvider,
            dietOrderingService);

        var act = () => service.CreateOrderAsync(CreateRequest(), customerId: 44);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Brak opublikowanego snapshotu M2*2026-06-08*");

        orderRepository.Verify(repo => repo.InsertCheckoutAsync(
            It.IsAny<Order>(),
            It.IsAny<IReadOnlyCollection<OrderItem>>(),
            It.IsAny<IReadOnlyCollection<DeliveryCalendar>>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldRejectAddressThatDoesNotBelongToCustomer()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var addressRepository = CreateAddressRepository(customerId: 99);
        var dietDataProvider = new Mock<IDietDataProvider>();
        var dietOrderingService = new Mock<IDietOrderingService>();

        var service = CreateService(
            orderRepository,
            deliveryCalendarRepository,
            addressRepository,
            dietDataProvider,
            dietOrderingService);

        var act = () => service.CreateOrderAsync(CreateRequest(), customerId: 44);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*adres dostawy*nie nalezy*");
        dietOrderingService.Verify(service => service.CreateCartItemAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        orderRepository.Verify(repo => repo.InsertCheckoutAsync(
            It.IsAny<Order>(),
            It.IsAny<IReadOnlyCollection<OrderItem>>(),
            It.IsAny<IReadOnlyCollection<DeliveryCalendar>>()), Times.Never);
    }

    private static OrderService CreateService(
        Mock<IOrderRepository> orderRepository,
        Mock<IDeliveryCalendarRepository> deliveryCalendarRepository,
        Mock<IAddressRepository> addressRepository,
        Mock<IDietDataProvider> dietDataProvider,
        Mock<IDietOrderingService> dietOrderingService)
        => new(
            orderRepository.Object,
            deliveryCalendarRepository.Object,
            addressRepository.Object,
            Mock.Of<IMapper>(),
            dietDataProvider.Object,
            dietOrderingService.Object);

    private static CreateOrderRequest CreateRequest()
        => new()
        {
            AddressId = 7,
            StartDate = new DateTime(2026, 6, 8),
            Items =
            [
                new CreateOrderItemRequest
                {
                    DietId = 5,
                    DietVariantId = 10,
                    DietName = "Dieta Sport",
                    VariantName = "2200 kcal",
                    CaloriesPerDay = 2200,
                    PricePerDay = 99.99m,
                    TotalDays = 1,
                },
            ],
        };

    private static Mock<IAddressRepository> CreateAddressRepository(int customerId)
    {
        var repository = new Mock<IAddressRepository>();
        repository.Setup(repo => repo.GetByIdAsync(7)).ReturnsAsync(new Address
        {
            Id = 7,
            UserId = customerId,
            Label = "Dom",
            Street = "Testowa",
            BuildingNumber = "1",
            City = "Warszawa",
            PostalCode = "00-001",
        });

        return repository;
    }

    private static Mock<IDietOrderingService> CreateDietOrderingService(decimal serverPricePerDay)
    {
        var service = new Mock<IDietOrderingService>();
        service
            .Setup(s => s.CreateCartItemAsync(10, It.IsAny<int>()))
            .ReturnsAsync((int _, int totalDays) => new CartItemDto
            {
                DietId = 5,
                DietVariantId = 10,
                DietName = "Dieta Sport",
                VariantName = "2200 kcal",
                CaloriesPerDay = 2200,
                PricePerDay = serverPricePerDay,
                TotalDays = totalDays,
            });

        return service;
    }

    private static PublishedDietPlanSnapshotDto CreateSnapshot(DateOnly date, int breakfastItemId, int dinnerItemId)
        => new()
        {
            DietMenuPlanId = date.DayNumber,
            PlanDate = date,
            PlanStatus = "Published",
            Items =
            [
                new PublishedDietPlanItemDto
                {
                    DietMenuPlanItemId = breakfastItemId,
                    DietMenuPlanId = date.DayNumber,
                    PlanDate = date,
                    MealId = 801,
                    MealVariantId = 901,
                    MealName = "Owsianka proteinowa",
                    DietVariantId = 10,
                    MealSlot = "Breakfast",
                    SortOrder = 1,
                },
                new PublishedDietPlanItemDto
                {
                    DietMenuPlanItemId = dinnerItemId,
                    DietMenuPlanId = date.DayNumber,
                    PlanDate = date,
                    MealId = 802,
                    MealName = "Kurczak z ryzem",
                    DietVariantId = 10,
                    MealSlot = "Dinner",
                    SortOrder = 2,
                },
                new PublishedDietPlanItemDto
                {
                    DietMenuPlanItemId = dinnerItemId + 50,
                    DietMenuPlanId = date.DayNumber,
                    PlanDate = date,
                    MealId = 803,
                    MealName = "Inny wariant",
                    DietVariantId = 11,
                    MealSlot = "Dinner",
                    SortOrder = 2,
                },
            ],
        };
}
