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

public sealed class M1CheckoutSmokeTests
{
    [Fact]
    public async Task DietSelection_Cart_Checkout_ShouldCreateOrderItemsWithM2References()
    {
        var startDate = new DateTime(2026, 6, 8);
        var selectedDietVariantId = 10;
        var catalogProvider = CreateCatalogProvider(selectedDietVariantId);
        var dietOrderingService = new DietOrderingService(catalogProvider.Object);
        var cartService = new CartService();

        var selectedCartItem = await dietOrderingService.CreateCartItemAsync(selectedDietVariantId, totalDays: 1);
        var cart = cartService.AddItem(new CartDto(), selectedCartItem);
        var checkoutCart = cartService.GetCart(cartService.Serialize(cart));
        var checkoutRequest = new CreateOrderRequest
        {
            AddressId = 7,
            DeliveryWindowId = 3,
            StartDate = startDate,
            Items = checkoutCart.Items.Select(item => new CreateOrderItemRequest
            {
                DietId = item.DietId,
                DietVariantId = item.DietVariantId,
                DietName = item.DietName,
                VariantName = item.VariantName,
                CaloriesPerDay = item.CaloriesPerDay,
                PricePerDay = item.PricePerDay,
                TotalDays = item.TotalDays,
            }).ToList(),
        };

        var orderRepository = new Mock<IOrderRepository>();
        var orderItemRepository = new Mock<IOrderItemRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var dietDataProvider = new Mock<IDietDataProvider>();
        var insertedItems = new List<OrderItem>();

        orderRepository.Setup(repo => repo.GenerateOrderNumberAsync()).ReturnsAsync("ORD-20260608-1");
        orderRepository.Setup(repo => repo.InsertAsync(It.IsAny<Order>())).ReturnsAsync(123);
        orderRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Order>())).ReturnsAsync(true);
        orderItemRepository
            .Setup(repo => repo.InsertAsync(It.IsAny<OrderItem>()))
            .Callback<OrderItem>(insertedItems.Add)
            .ReturnsAsync(0);
        deliveryCalendarRepository.Setup(repo => repo.InsertAsync(It.IsAny<DeliveryCalendar>())).ReturnsAsync(0);
        dietDataProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 8)))
            .ReturnsAsync(CreatePublishedSnapshot());

        var orderService = new OrderService(
            orderRepository.Object,
            orderItemRepository.Object,
            deliveryCalendarRepository.Object,
            Mock.Of<IAddressRepository>(),
            Mock.Of<IMapper>(),
            dietDataProvider.Object);

        var orderId = await orderService.CreateOrderAsync(checkoutRequest, customerId: 44);

        orderId.Should().Be(123);
        checkoutCart.Items.Should().ContainSingle(item =>
            item.DietId == 5 &&
            item.DietVariantId == selectedDietVariantId &&
            item.DietName == "Dieta Sport" &&
            item.VariantName == "2200 kcal");
        insertedItems.Should().HaveCount(2);
        insertedItems.Should().OnlyContain(item =>
            item.OrderId == 123 &&
            item.DietId == 5 &&
            item.DietVariantId == selectedDietVariantId &&
            item.DeliveryDate == startDate.Date &&
            item.TotalDays == 1);

        insertedItems[0].DietMenuPlanItemId.Should().Be(1001);
        insertedItems[0].MealId.Should().Be(801);
        insertedItems[0].MealVariantId.Should().Be(901);
        insertedItems[0].MealSlot.Should().Be("Breakfast");

        insertedItems[1].DietMenuPlanItemId.Should().Be(1002);
        insertedItems[1].MealId.Should().Be(802);
        insertedItems[1].MealVariantId.Should().Be(902);
        insertedItems[1].MealSlot.Should().Be("Lunch");

        catalogProvider.Verify(provider => provider.GetVariantAsync(selectedDietVariantId), Times.Once);
        catalogProvider.Verify(provider => provider.GetDietAsync(5), Times.Once);
        dietDataProvider.Verify(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 8)), Times.Once);
        orderItemRepository.Verify(repo => repo.InsertAsync(It.IsAny<OrderItem>()), Times.Exactly(2));
    }

    private static Mock<IDietCatalogProvider> CreateCatalogProvider(int selectedDietVariantId)
    {
        var provider = new Mock<IDietCatalogProvider>();
        provider.Setup(p => p.GetVariantAsync(selectedDietVariantId)).ReturnsAsync(new DietCatalogVariantDto
        {
            DietVariantId = selectedDietVariantId,
            DietId = 5,
            Name = "2200 kcal",
            TargetCalories = 2200,
            PriceMultiplier = 1.10m,
            IsAvailable = true,
        });
        provider.Setup(p => p.GetDietAsync(5)).ReturnsAsync(new DietCatalogItemDto
        {
            DietId = 5,
            Name = "Dieta Sport",
            Status = "Published",
            IsActive = true,
            Variants =
            [
                new DietCatalogVariantDto
                {
                    DietVariantId = selectedDietVariantId,
                    DietId = 5,
                    Name = "2200 kcal",
                    TargetCalories = 2200,
                    PriceMultiplier = 1.10m,
                    IsDefault = true,
                    IsAvailable = true,
                },
            ],
        });

        return provider;
    }

    private static PublishedDietPlanSnapshotDto CreatePublishedSnapshot()
        => new()
        {
            DietMenuPlanId = 77,
            PlanDate = new DateOnly(2026, 6, 8),
            PlanStatus = "Published",
            Items =
            [
                new PublishedDietPlanItemDto
                {
                    DietMenuPlanItemId = 1001,
                    DietMenuPlanId = 77,
                    PlanDate = new DateOnly(2026, 6, 8),
                    MealId = 801,
                    MealVariantId = 901,
                    MealName = "Owsianka proteinowa",
                    DietVariantId = 10,
                    MealSlot = "Breakfast",
                    SortOrder = 1,
                },
                new PublishedDietPlanItemDto
                {
                    DietMenuPlanItemId = 1002,
                    DietMenuPlanId = 77,
                    PlanDate = new DateOnly(2026, 6, 8),
                    MealId = 802,
                    MealVariantId = 902,
                    MealName = "Kurczak z ryzem",
                    DietVariantId = 10,
                    MealSlot = "Lunch",
                    SortOrder = 2,
                },
            ],
        };
}
