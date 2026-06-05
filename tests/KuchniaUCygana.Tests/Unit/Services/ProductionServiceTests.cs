using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class ProductionServiceTests
{
    [Fact]
    public async Task ApproveCookingAsync_ShouldBlock_WhenPlanItemHasNoM2Snapshot()
    {
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 100,
                MealName = "Test meal",
                DietVariantId = 1,
                PlannedQuantity = 10,
                M2SnapshotJson = null,
            });
        var service = CreateService(itemRepository: itemRepository);

        var act = () => service.ApproveCookingAsync(21, 10m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Brak snapshotu M2*");
        itemRepository.Verify(repository => repository.UpdateAsync(It.IsAny<ProductionPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task ProduceSemiFinishedAsync_ShouldBlock_WhenPublishedM2SnapshotIsMissing()
    {
        var planRepository = new Mock<IProductionPlanRepository>();
        planRepository
            .Setup(repository => repository.GetByIdAsync(5))
            .ReturnsAsync(new ProductionPlan
            {
                Id = 5,
                ProductionDate = new DateOnly(2026, 6, 5),
            });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(5))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 21,
                    ProductionPlanId = 5,
                    MealId = 100,
                    MealName = "Test meal",
                    DietVariantId = 1,
                    PlannedQuantity = 10,
                    M2SnapshotJson = null,
                },
            });
        var dietProvider = new Mock<IDietDataProvider>();
        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(new DateOnly(2026, 6, 5)))
            .ReturnsAsync((PublishedDietPlanSnapshotDto?)null);
        var service = CreateService(planRepository: planRepository, dietProvider: dietProvider);

        var act = () => service.ProduceSemiFinishedAsync(5);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Brak opublikowanego snapshotu M2*");
        dietProvider.Verify(provider => provider.GetRecipeForMealAsync(It.IsAny<int>()), Times.Never);
        planRepository.Verify(repository => repository.UpdateAsync(It.IsAny<ProductionPlan>()), Times.Never);
    }

    private static ProductionService CreateService(
        Mock<IProductionPlanRepository>? planRepository = null,
        Mock<IRepository<ProductionPlanItem>>? itemRepository = null,
        Mock<IDietDataProvider>? dietProvider = null)
    {
        planRepository ??= new Mock<IProductionPlanRepository>();
        itemRepository ??= new Mock<IRepository<ProductionPlanItem>>();
        dietProvider ??= new Mock<IDietDataProvider>();

        var orderProvider = new Mock<IOrderDataProvider>();
        var planGenerator = new ProductionPlanGenerator(
            orderProvider.Object,
            dietProvider.Object,
            new FoodCostCalculator(dietProvider.Object),
            planRepository.Object);
        var fefoService = new FefoService(
            Mock.Of<IBatchRepository>(),
            Mock.Of<IWarehouseCommandRepository>());

        return new ProductionService(
            planGenerator,
            planRepository.Object,
            itemRepository.Object,
            dietProvider.Object,
            Mock.Of<IPackingService>(),
            fefoService,
            Mock.Of<IMapper>(),
            Mock.Of<ILogger<ProductionService>>());
    }
}
