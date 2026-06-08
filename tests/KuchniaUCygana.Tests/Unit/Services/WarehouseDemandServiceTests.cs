using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using System.Text.Json;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class WarehouseDemandServiceTests
{
    [Fact]
    public async Task GetDemandAsync_ShouldAggregateIngredientsAndPackaging_FromPaidOrdersAndM2Snapshot()
    {
        var date = new DateOnly(2026, 6, 8);
        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(CreateSnapshot(date));
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, 901, 1001, "Lunch")),
                CreateDelivery(date, 2, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, 901, 1001, "Lunch")),
            });
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByStockItemAsync(7001))
            .ReturnsAsync(new[]
            {
                new Batch { Id = 1, StockItemId = 7001, CurrentQuantity = 180m, ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(1) },
            });
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByStockItemAsync(8001))
            .ReturnsAsync(new[]
            {
                new Batch { Id = 2, StockItemId = 8001, CurrentQuantity = 10m, ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(30) },
            });

        var service = new WarehouseDemandService(dietProvider.Object, orderProvider.Object, batchRepository.Object);

        var demand = await service.GetDemandAsync(date, 7);

        demand.TotalOrderItems.Should().Be(2);
        demand.Rows.Should().ContainSingle(row =>
            row.ResourceType == "Ingredient" &&
            row.ResourceName == "Kurczak" &&
            row.StockItemId == 7001 &&
            row.RequiredQuantity == 200m &&
            row.AvailableQuantity == 180m &&
            row.ShortageQuantity == 20m);
        demand.Rows.Should().ContainSingle(row =>
            row.ResourceType == "Packaging" &&
            row.ResourceName == "Pudelko obiadowe" &&
            row.StockItemId == 8001 &&
            row.RequiredQuantity == 2m &&
            row.AvailableQuantity == 10m &&
            row.ShortageQuantity == 0m);
        demand.Days.Should().ContainSingle(day =>
            day.PlanDate == date &&
            day.Status == "Published" &&
            day.OrderItemCount == 2 &&
            day.SnapshotItemCount == 1);
    }

    [Fact]
    public async Task GetDemandAsync_ShouldPreviewCategoryFefo_WhenIngredientHasNoStockItemId()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        snapshot.Items.Single().AggregateIngredients = new[]
        {
            new AggregateIngredientDto
            {
                IngredientId = 12,
                IngredientName = "Warzywa mix",
                WarehouseCategoryId = 77,
                WarehouseCategoryName = "Warzywa rownowazne",
                NetWeightInGrams = 150m,
            },
        };
        snapshot.Items.Single().PackagingRequirements = Array.Empty<PackagingRequirementDto>();

        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, null, 1001, "Lunch")),
            });
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByWarehouseCategoryAsync(77))
            .ReturnsAsync(new[]
            {
                new Batch { Id = 3, StockItemId = 7007, CurrentQuantity = 90m, ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(2) },
            });

        var service = new WarehouseDemandService(dietProvider.Object, orderProvider.Object, batchRepository.Object);

        var demand = await service.GetDemandAsync(date, 1);

        demand.Rows.Should().ContainSingle(row =>
            row.SelectionMode == "WarehouseCategory" &&
            row.WarehouseCategoryId == 77 &&
            row.RequiredQuantity == 150m &&
            row.AvailableQuantity == 90m &&
            row.ShortageQuantity == 60m &&
            row.RiskLabel == "Shortage");
    }

    [Fact]
    public async Task GetDemandAsync_ShouldReuseCategoryAvailabilityWithinSingleReport()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        snapshot.Items.Single().AggregateIngredients = new[]
        {
            new AggregateIngredientDto
            {
                IngredientId = 12,
                IngredientName = "Warzywa mix",
                WarehouseCategoryId = 77,
                WarehouseCategoryName = "Warzywa rownowazne",
                NetWeightInGrams = 150m,
            },
            new AggregateIngredientDto
            {
                IngredientId = 13,
                IngredientName = "Zielone dodatki",
                WarehouseCategoryId = 77,
                WarehouseCategoryName = "Warzywa rownowazne",
                NetWeightInGrams = 40m,
            },
        };
        snapshot.Items.Single().PackagingRequirements = Array.Empty<PackagingRequirementDto>();

        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, null, 1001, "Lunch")),
            });
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByWarehouseCategoryAsync(77))
            .ReturnsAsync(new[]
            {
                new Batch { Id = 3, StockItemId = 7007, CurrentQuantity = 300m, ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(2) },
            });

        var service = new WarehouseDemandService(dietProvider.Object, orderProvider.Object, batchRepository.Object);

        var demand = await service.GetDemandAsync(date, 1);

        demand.Rows.Should().HaveCount(2);
        batchRepository.Verify(
            repository => repository.GetActiveBatchesByWarehouseCategoryAsync(77),
            Times.Once);
    }

    [Fact]
    public async Task GetDemandAsync_ShouldNotDoubleCountSameBatchBetweenStockItemAndCategoryPreview()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        snapshot.Items.Single().AggregateIngredients = new[]
        {
            new AggregateIngredientDto
            {
                IngredientId = 12,
                IngredientName = "Kurczak filet",
                StockItemId = 7001,
                WarehouseCategoryId = 77,
                WarehouseCategoryName = "Mieso rownowazne",
                NetWeightInGrams = 80m,
            },
            new AggregateIngredientDto
            {
                IngredientId = 13,
                IngredientName = "Mieso zamienne",
                WarehouseCategoryId = 77,
                WarehouseCategoryName = "Mieso rownowazne",
                NetWeightInGrams = 50m,
            },
        };
        snapshot.Items.Single().PackagingRequirements = Array.Empty<PackagingRequirementDto>();

        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, null, 1001, "Lunch")),
            });
        var sharedBatch = new Batch
        {
            Id = 3,
            StockItemId = 7001,
            CurrentQuantity = 100m,
            ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(2),
        };
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByStockItemAsync(7001))
            .ReturnsAsync(new[] { sharedBatch });
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByWarehouseCategoryAsync(77))
            .ReturnsAsync(new[] { sharedBatch });

        var service = new WarehouseDemandService(dietProvider.Object, orderProvider.Object, batchRepository.Object);

        var demand = await service.GetDemandAsync(date, 1);

        demand.Rows.Should().ContainSingle(row =>
            row.SelectionMode == "StockItem" &&
            row.StockItemId == 7001 &&
            row.RequiredQuantity == 80m &&
            row.AvailableQuantity == 100m &&
            row.ShortageQuantity == 0m);
        demand.Rows.Should().ContainSingle(row =>
            row.SelectionMode == "WarehouseCategory" &&
            row.WarehouseCategoryId == 77 &&
            row.RequiredQuantity == 50m &&
            row.AvailableQuantity == 20m &&
            row.ShortageQuantity == 30m);
    }

    [Fact]
    public async Task GetDemandAsync_ShouldExposeMissingWarehouseMapping_InsteadOfDroppingRow()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        snapshot.Items.Single().AggregateIngredients = new[]
        {
            new AggregateIngredientDto
            {
                IngredientId = 99,
                IngredientName = "Demo skladnik bez mapowania",
                NetWeightInGrams = 120m,
            },
        };
        snapshot.Items.Single().PackagingRequirements = Array.Empty<PackagingRequirementDto>();

        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, 901, 1001, "Lunch")),
            });

        var service = new WarehouseDemandService(dietProvider.Object, orderProvider.Object, batchRepository.Object);

        var demand = await service.GetDemandAsync(date, 1);

        demand.Rows.Should().ContainSingle(row =>
            row.ResourceType == "Ingredient" &&
            row.ResourceName == "Demo skladnik bez mapowania" &&
            row.SelectionMode == "MissingMapping" &&
            row.RiskLabel == "MissingMapping" &&
            row.RequiredQuantity == 120m &&
            row.ShortageQuantity == 120m);
        batchRepository.Verify(
            repository => repository.GetActiveBatchesByStockItemAsync(It.IsAny<int>()),
            Times.Never);
        batchRepository.Verify(
            repository => repository.GetActiveBatchesByWarehouseCategoryAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDemandAsync_ShouldUseFrozenProductionSnapshot_WhenProductionPlanExists()
    {
        var date = new DateOnly(2026, 6, 8);
        var liveSnapshot = CreateSnapshot(date);
        var frozenSnapshotItem = CreateSnapshot(date).Items.Single();
        frozenSnapshotItem.DietMenuPlanItemId = 2001;
        frozenSnapshotItem.MealName = "Ryż z warzywami";
        frozenSnapshotItem.AggregateIngredients = new[]
        {
            new AggregateIngredientDto
            {
                IngredientId = 33,
                IngredientName = "Ryż",
                StockItemId = 7333,
                WarehouseCategoryId = 71,
                WarehouseCategoryName = "Produkty sypkie",
                NetWeightInGrams = 250m,
            },
        };
        frozenSnapshotItem.PackagingRequirements = Array.Empty<PackagingRequirementDto>();

        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();
        var planRepository = new Mock<IProductionPlanRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(liveSnapshot);
        planRepository
            .Setup(repository => repository.GetByDateAsync(date))
            .ReturnsAsync(new ProductionPlan { Id = 90, ProductionDate = date });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(90))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 91,
                    ProductionPlanId = 90,
                    DietMenuPlanItemId = 2001,
                    MealId = frozenSnapshotItem.MealId,
                    DietVariantId = frozenSnapshotItem.DietVariantId,
                    PlannedQuantity = 3,
                    M2SnapshotJson = JsonSerializer.Serialize(frozenSnapshotItem),
                },
            });
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, 901, 1001, "Lunch")),
            });
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByStockItemAsync(7333))
            .ReturnsAsync(new[]
            {
                new Batch { Id = 7, StockItemId = 7333, CurrentQuantity = 1000m },
            });

        var service = new WarehouseDemandService(
            dietProvider.Object,
            orderProvider.Object,
            batchRepository.Object,
            planRepository.Object);

        var demand = await service.GetDemandAsync(date, 1);

        demand.Rows.Should().ContainSingle(row =>
            row.ResourceType == "Ingredient" &&
            row.ResourceName == "Ryż" &&
            row.StockItemId == 7333 &&
            row.RequiredQuantity == 750m);
        demand.Rows.Should().NotContain(row => row.ResourceName == "Kurczak");
        demand.Days.Should().ContainSingle(day =>
            day.PlanDate == date &&
            day.Status == "ProductionSnapshot" &&
            day.SnapshotItemCount == 1 &&
            day.OrderItemCount == 3 &&
            day.MatchedOrderItemCount == 3);
    }

    [Fact]
    public async Task GetDemandAsync_ShouldReturnAllFilteredRows_WhenExportAllIsRequested()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        snapshot.Items.Single().AggregateIngredients = Enumerable.Range(1, 12)
            .Select(index => new AggregateIngredientDto
            {
                IngredientId = 100 + index,
                IngredientName = $"Skladnik {index:00}",
                StockItemId = 7000 + index,
                WarehouseCategoryId = 70,
                WarehouseCategoryName = "Demo",
                NetWeightInGrams = 10m,
            })
            .ToArray();
        snapshot.Items.Single().PackagingRequirements = Array.Empty<PackagingRequirementDto>();

        var dietProvider = new Mock<IDietDataProvider>();
        var orderProvider = new Mock<IOrderDataProvider>();
        var batchRepository = new Mock<IBatchRepository>();

        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);
        orderProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(date.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(new[]
            {
                CreateDelivery(date, 1, new OrderItemInfo(1, "Standard", 10, "2000", 2000, 501, 901, 1001, "Lunch")),
            });

        foreach (var ingredient in snapshot.Items.Single().AggregateIngredients)
        {
            batchRepository
                .Setup(repository => repository.GetActiveBatchesByStockItemAsync(ingredient.StockItemId!.Value))
                .ReturnsAsync(new[]
                {
                    new Batch
                    {
                        Id = ingredient.StockItemId.Value,
                        StockItemId = ingredient.StockItemId.Value,
                        CurrentQuantity = 100m,
                        ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(5),
                    },
                });
        }

        var service = new WarehouseDemandService(dietProvider.Object, orderProvider.Object, batchRepository.Object);

        var demand = await service.GetDemandAsync(new WarehouseDemandFilterDto
        {
            StartDate = date,
            Days = 1,
            Page = 1,
            PageSize = 10,
            ExportAll = true,
        });

        demand.FilteredRows.Should().Be(12);
        demand.Rows.Should().HaveCount(12);
        demand.PageSize.Should().Be(12);
    }

    private static PublishedDietPlanSnapshotDto CreateSnapshot(DateOnly date)
        => new()
        {
            DietMenuPlanId = 77,
            PlanDate = date,
            PlanStatus = "Published",
            PublishedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new PublishedDietPlanItemDto
                {
                    DietMenuPlanId = 77,
                    DietMenuPlanItemId = 1001,
                    PlanDate = date,
                    MealId = 501,
                    MealVariantId = 901,
                    MealVariantName = "Standard",
                    MealName = "Kurczak z ryzem",
                    DietVariantId = 10,
                    MealSlot = "Lunch",
                    SortOrder = 1,
                    ServingMultiplier = 1m,
                    IsCompleteForProduction = true,
                    CompletenessStatus = "Complete",
                    AggregateIngredients =
                    [
                        new AggregateIngredientDto
                        {
                            IngredientId = 11,
                            IngredientName = "Kurczak",
                            StockItemId = 7001,
                            WarehouseCategoryId = 70,
                            WarehouseCategoryName = "Mieso",
                            NetWeightInGrams = 100m,
                        },
                    ],
                    PackagingRequirements =
                    [
                        new PackagingRequirementDto
                        {
                            ResourceName = "Pudelko obiadowe",
                            StockItemId = 8001,
                            Quantity = 1m,
                            Unit = "pcs",
                            IsCustomerFacing = true,
                        },
                    ],
                },
            ],
        };

    private static OrderDeliveryInfo CreateDelivery(DateOnly date, int orderId, params OrderItemInfo[] items)
        => new(
            orderId,
            orderId,
            $"ORD/{orderId}",
            orderId,
            null,
            $"Client {orderId}",
            "Test 1",
            "Warszawa",
            "00-001",
            null,
            null,
            date.ToDateTime(TimeOnly.MinValue),
            "Rano",
            items);
}
