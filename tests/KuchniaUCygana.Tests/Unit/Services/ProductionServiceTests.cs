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
    public async Task GeneratePlanAsync_ShouldKeepMealVariantsAsSeparateProductionItems_WhenPlanItemIdsAreProvided()
    {
        var date = new DateOnly(2026, 6, 5);
        var (generator, planRepository) = CreatePlanGenerator(CreateSnapshot(date));
        SetupDeliveries(
            generator.OrderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, 1001, "lunch"),
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 102, 1002, "lunch"));

        var result = await generator.Instance.GeneratePlanAsync(date, "test");

        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.DietMenuPlanItemId).Should().BeEquivalentTo(new[] { 1001, 1002 });
        result.Items.Select(item => item.RecipeComponentVersionIds).Should().BeEquivalentTo(new[] { "501", "502" });
        result.Items.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.M2SnapshotJson));
        result.FoodCostReport!.Entries.Should().ContainSingle(entry =>
            entry.StockItemId == 9001 && entry.TotalWeightGrams == 100m);
        result.FoodCostReport!.Entries.Should().ContainSingle(entry =>
            entry.StockItemId == 9002 && entry.TotalWeightGrams == 75m);
        planRepository.Verify(repository => repository.InsertAsync(It.IsAny<ProductionPlan>()), Times.Once);
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldKeepMealVariantsAsSeparateProductionItems_WhenOnlyMealVariantFallbackIsProvided()
    {
        var date = new DateOnly(2026, 6, 5);
        var (generator, _) = CreatePlanGenerator(CreateSnapshot(date));
        SetupDeliveries(
            generator.OrderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, null, "lunch"),
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 102, null, "lunch"));

        var result = await generator.Instance.GeneratePlanAsync(date, "test");

        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.DietMenuPlanItemId).Should().BeEquivalentTo(new[] { 1001, 1002 });
        result.Items.Select(item => item.MealName).Should().BeEquivalentTo(new[] { "Makaron standard", "Makaron sport" });
    }

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

    private static (GeneratorHarness generator, Mock<IProductionPlanRepository> planRepository) CreatePlanGenerator(
        PublishedDietPlanSnapshotDto snapshot)
    {
        var orderProvider = new Mock<IOrderDataProvider>();
        orderProvider
            .Setup(provider => provider.GetActiveOrdersAsync(snapshot.PlanDate))
            .ReturnsAsync(new[]
            {
                new ActiveOrderEntry
                {
                    OrderId = 1,
                    DietVariantId = 1,
                    DeliveryDate = snapshot.PlanDate,
                    ClientName = "Test",
                },
            });

        var dietProvider = new Mock<IDietDataProvider>();
        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(snapshot.PlanDate))
            .ReturnsAsync(snapshot);

        var planRepository = new Mock<IProductionPlanRepository>();
        planRepository
            .Setup(repository => repository.GetByDateAsync(snapshot.PlanDate))
            .ReturnsAsync((ProductionPlan?)null);
        planRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<ProductionPlan>()))
            .ReturnsAsync(77);

        var generator = new ProductionPlanGenerator(
            orderProvider.Object,
            dietProvider.Object,
            new FoodCostCalculator(dietProvider.Object),
            planRepository.Object);

        return (new GeneratorHarness(generator, orderProvider), planRepository);
    }

    private static void SetupDeliveries(
        Mock<IOrderDataProvider> orderProvider,
        DateOnly date,
        params OrderItemInfo[] items)
    {
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                new OrderDeliveryInfo(
                    1,
                    1,
                    "ORD/1",
                    1,
                    null,
                    "Test Client",
                    "Test 1",
                    "Warszawa",
                    "00-001",
                    null,
                    null,
                    date.ToDateTime(TimeOnly.MinValue),
                    "Rano",
                    items),
            });
    }

    private static PublishedDietPlanSnapshotDto CreateSnapshot(DateOnly date)
        => new()
        {
            DietMenuPlanId = 50,
            PlanDate = date,
            PlanStatus = "Published",
            Items = new[]
            {
                CreateSnapshotItem(date, 1001, 101, "Makaron standard", 501, 9001, 100m, 1.0m),
                CreateSnapshotItem(date, 1002, 102, "Makaron sport", 502, 9002, 50m, 1.5m),
            },
        };

    private static PublishedDietPlanItemDto CreateSnapshotItem(
        DateOnly date,
        int dietMenuPlanItemId,
        int mealVariantId,
        string mealName,
        int recipeComponentVersionId,
        int stockItemId,
        decimal ingredientWeight,
        decimal servingMultiplier)
        => new()
        {
            DietMenuPlanItemId = dietMenuPlanItemId,
            DietMenuPlanId = 50,
            PlanDate = date,
            MealId = 10,
            MealVariantId = mealVariantId,
            MealVariantName = $"Wariant {mealVariantId}",
            MealName = mealName,
            DietVariantId = 1,
            MealSlot = "lunch",
            SortOrder = mealVariantId == 101 ? 1 : 2,
            ServingMultiplier = servingMultiplier,
            FinalWeightGrams = 300m,
            FinalWeightAfterMultiplierGrams = 300m * servingMultiplier,
            NutritionSource = "Aggregated",
            CompletenessStatus = "Complete",
            IsCompleteForProduction = true,
            RecipeComponentVersionIds = new[] { recipeComponentVersionId },
            AggregateIngredients = new[]
            {
                new AggregateIngredientDto
                {
                    IngredientId = stockItemId,
                    IngredientName = $"Skladnik {stockItemId}",
                    StockItemId = stockItemId,
                    WarehouseCategoryId = 700,
                    NetWeightInGrams = ingredientWeight,
                    GrossWeightInGrams = ingredientWeight,
                    SourceRecipeComponentVersionIds = new[] { recipeComponentVersionId },
                    SourceComponentNames = new[] { $"Skladowa {recipeComponentVersionId}" },
                },
            },
            Components = new[]
            {
                new MealComponentVersionDto
                {
                    RecipeComponentId = recipeComponentVersionId,
                    RecipeComponentVersionId = recipeComponentVersionId,
                    ComponentName = $"Skladowa {recipeComponentVersionId}",
                    VersionNumber = 1,
                    VersionStatus = "Published",
                    QuantityPerServing = 1m,
                    Unit = "portion",
                    YieldQuantity = 1m,
                },
            },
        };

    private sealed record GeneratorHarness(
        ProductionPlanGenerator Instance,
        Mock<IOrderDataProvider> OrderProvider);
}
