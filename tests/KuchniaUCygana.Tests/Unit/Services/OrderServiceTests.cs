using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Orders;
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
        var orderItemRepository = new Mock<IOrderItemRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var dietDataProvider = new Mock<IDietDataProvider>();
        var insertedItems = new List<OrderItem>();

        orderRepository.Setup(repo => repo.GenerateOrderNumberAsync()).ReturnsAsync("ORD-20260608-1");
        orderRepository.Setup(repo => repo.InsertAsync(It.IsAny<Order>())).ReturnsAsync(123);
        orderItemRepository
            .Setup(repo => repo.InsertAsync(It.IsAny<OrderItem>()))
            .Callback<OrderItem>(insertedItems.Add)
            .ReturnsAsync(0);
        deliveryCalendarRepository.Setup(repo => repo.InsertAsync(It.IsAny<DeliveryCalendar>())).ReturnsAsync(0);
        orderRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(true);

        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 8)))
            .ReturnsAsync(CreateSnapshot(new DateOnly(2026, 6, 8), 1001, 1002));
        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 9)))
            .ReturnsAsync(CreateSnapshot(new DateOnly(2026, 6, 9), 2001, 2002));

        var service = CreateService(
            orderRepository,
            orderItemRepository,
            deliveryCalendarRepository,
            dietDataProvider);

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
                    DietName = "Dieta Sport",
                    VariantName = "2200 kcal",
                    CaloriesPerDay = 2200,
                    PricePerDay = 99.99m,
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
        insertedItems.Where(item => item.DeliveryDate == startDate.Date).Should().HaveCount(2);
        insertedItems.Where(item => item.DeliveryDate == startDate.Date.AddDays(1)).Should().HaveCount(2);
        insertedItems.GroupBy(item => item.DeliveryDate)
            .Should().OnlyContain(group => group.Sum(item => item.TotalPrice) == 99.99m);

        orderRepository.Verify(repo => repo.InsertAsync(It.Is<Order>(order =>
            order.TotalPrice == 199.98m &&
            order.FinalPrice == 199.98m &&
            order.StartDate == startDate)), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldRejectCheckoutWhenPublishedM2PlanIsMissing()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var orderItemRepository = new Mock<IOrderItemRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var dietDataProvider = new Mock<IDietDataProvider>();

        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 8)))
            .ReturnsAsync((PublishedDietPlanSnapshotDto?)null);

        var service = CreateService(
            orderRepository,
            orderItemRepository,
            deliveryCalendarRepository,
            dietDataProvider);

        var act = () => service.CreateOrderAsync(new CreateOrderRequest
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
        }, customerId: 44);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Brak opublikowanego snapshotu M2*2026-06-08*");

        orderRepository.Verify(repo => repo.InsertAsync(It.IsAny<Order>()), Times.Never);
        orderItemRepository.Verify(repo => repo.InsertAsync(It.IsAny<OrderItem>()), Times.Never);
        deliveryCalendarRepository.Verify(repo => repo.InsertAsync(It.IsAny<DeliveryCalendar>()), Times.Never);
    }

    private static OrderService CreateService(
        Mock<IOrderRepository> orderRepository,
        Mock<IOrderItemRepository> orderItemRepository,
        Mock<IDeliveryCalendarRepository> deliveryCalendarRepository,
        Mock<IDietDataProvider> dietDataProvider)
        => new(
            orderRepository.Object,
            orderItemRepository.Object,
            deliveryCalendarRepository.Object,
            Mock.Of<IAddressRepository>(),
            Mock.Of<IMapper>(),
            dietDataProvider.Object);

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
