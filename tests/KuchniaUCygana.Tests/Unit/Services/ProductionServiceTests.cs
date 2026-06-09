using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
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
    public async Task GeneratePlanAsync_ShouldRejectExplicitOrderItem_WhenM2SnapshotDoesNotContainIt()
    {
        var date = new DateOnly(2026, 6, 5);
        var (generator, _) = CreatePlanGenerator(CreateSnapshot(date));
        SetupDeliveries(
            generator.OrderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 99, 999, 9999, "lunch"));

        var act = () => generator.Instance.GeneratePlanAsync(date, "test");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*snapshotu M2*MealId 99*DietMenuPlanItemId 9999*");
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldRejectStaleDietMenuPlanItemId_EvenWhenMealVariantMatches()
    {
        var date = new DateOnly(2026, 6, 5);
        var (generator, _) = CreatePlanGenerator(CreateSnapshot(date));
        SetupDeliveries(
            generator.OrderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, 9999, "lunch"));

        var act = () => generator.Instance.GeneratePlanAsync(date, "test");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*snapshotu M2*MealId 10*DietMenuPlanItemId 9999*");
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
    public async Task ApproveCookingAsync_ShouldBlock_WhenFefoWasNotDeducted()
    {
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 10,
                M2SnapshotJson = JsonSerializer.Serialize(CreateSnapshotItem(
                    new DateOnly(2026, 6, 5),
                    1001,
                    101,
                    "Makaron standard",
                    501,
                    9001,
                    100m,
                    1m)),
            });
        var service = CreateService(itemRepository: itemRepository);

        var act = () => service.ApproveCookingAsync(21, 10m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FEFO*");
        itemRepository.Verify(repository => repository.UpdateAsync(It.IsAny<ProductionPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task ApproveCookingAsync_ShouldBlock_WhenRequiredComponentSessionIsNotCompleted()
    {
        var snapshotItem = CreateSnapshotItem(
            new DateOnly(2026, 6, 5),
            1001,
            101,
            "Makaron standard",
            501,
            9001,
            100m,
            1m);
        snapshotItem.Components.Single().InstructionSections = new[]
        {
            new ComponentInstructionSectionDto
            {
                SectionId = 71,
                Title = "Kontrola",
                SortOrder = 1,
                Steps = new[]
                {
                    new ComponentInstructionStepDto
                    {
                        StepId = 711,
                        StepText = "Sprawdz temperature rdzenia.",
                        RequiresControl = true,
                        IsCritical = true,
                    },
                },
            },
        };
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 10,
                FefoDeductedAt = DateTimeOffset.UtcNow,
                PackagingDeductedAt = DateTimeOffset.UtcNow,
                M2SnapshotJson = JsonSerializer.Serialize(snapshotItem),
            });
        var cookingSessionService = new Mock<ICookingSessionService>();
        cookingSessionService
            .Setup(service => service.GetComponentSessionAsync(21, 501))
            .ReturnsAsync(new CookingComponentSessionDto
            {
                Status = "InProgress",
                StepChecksByStepId = new()
                {
                    [711] = new CookingStepCheckDto { StepId = 711, Status = "Checked" },
                },
            });
        var service = CreateService(
            itemRepository: itemRepository,
            cookingSessionService: cookingSessionService);

        var act = () => service.ApproveCookingAsync(21, 10m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*skladowe*");
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

    [Fact]
    public async Task ProduceSemiFinishedAsync_ShouldRegisterFefoWithoutMovingItemToCooking()
    {
        var date = new DateOnly(2026, 6, 5);
        var item = new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            MealId = 10,
            MealName = "Makaron standard",
            DietVariantId = 1,
            PlannedQuantity = 2,
            Status = ProductionItemStatus.Planned,
            M2SnapshotJson = JsonSerializer.Serialize(CreateSnapshotItem(date, 1001, 101, "Makaron standard", 501, 9001, 100m, 1m)),
        };
        var planRepository = new Mock<IProductionPlanRepository>();
        planRepository
            .Setup(repository => repository.GetByIdAsync(5))
            .ReturnsAsync(new ProductionPlan
            {
                Id = 5,
                ProductionDate = date,
            });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(5))
            .ReturnsAsync(new[] { item });

        var batchRepository = new Mock<IBatchRepository>();
        batchRepository
            .Setup(repository => repository.GetActiveBatchesByStockItemAsync(9001))
            .ReturnsAsync(new[]
            {
                new Batch
                {
                    Id = 7,
                    StockItemId = 9001,
                    CurrentQuantity = 500m,
                    ExpiryDate = date.ToDateTime(TimeOnly.MinValue).AddDays(2),
                },
            });
        var warehouseCommandRepository = new Mock<IWarehouseCommandRepository>();
        warehouseCommandRepository
            .Setup(repository => repository.DeductStockAsync(It.IsAny<WarehouseDeductionCommand>()))
            .ReturnsAsync(new[]
            {
                new InventoryTransaction
                {
                    BatchId = 7,
                    StockItemId = 9001,
                    QuantityChanged = -200m,
                },
            });
        var service = CreateService(
            planRepository: planRepository,
            fefoService: new FefoService(batchRepository.Object, warehouseCommandRepository.Object));

        await service.ProduceSemiFinishedAsync(5);

        item.FefoDeductedAt.Should().NotBeNull();
        item.Status.Should().Be(ProductionItemStatus.Planned);
        warehouseCommandRepository.Verify(
            repository => repository.DeductStockAsync(It.Is<WarehouseDeductionCommand>(command =>
                command.StockItemId == 9001 &&
                command.Quantity == 200m)),
            Times.Once);
    }

    [Fact]
    public async Task GetCookingComponentCardAsync_ShouldUseSnapshotInstructions_AndScaleIngredientQuantities()
    {
        var date = new DateOnly(2026, 6, 5);
        var snapshotItem = CreateSnapshotItem(date, 1001, 101, "Makaron standard", 501, 9001, 100m, 1.5m);
        var component = snapshotItem.Components.Single();
        component.Ingredients = new[]
        {
            new ComponentIngredientDto
            {
                IngredientId = 11,
                IngredientName = "Kurczak",
                StockItemId = 9001,
                WarehouseCategoryName = "Mieso",
                WeightInGrams = 120m,
            },
        };
        component.InstructionSections = new[]
        {
            new ComponentInstructionSectionDto
            {
                SectionId = 71,
                Title = "Obrobka",
                SortOrder = 1,
                Steps = new[]
                {
                    new ComponentInstructionStepDto
                    {
                        StepId = 711,
                        StepText = "Podgrzej do temperatury rdzenia.",
                        SortOrder = 1,
                        RequiresControl = true,
                        ControlType = "CoreTemperature",
                        ExpectedValue = 75m,
                        ExpectedUnit = "C",
                        IsCritical = true,
                    },
                },
            },
        };

        var planRepository = new Mock<IProductionPlanRepository>();
        planRepository
            .Setup(repository => repository.GetByIdAsync(5))
            .ReturnsAsync(new ProductionPlan { Id = 5, ProductionDate = date });
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 7,
                FefoDeductedAt = DateTimeOffset.UtcNow,
                M2SnapshotJson = JsonSerializer.Serialize(snapshotItem),
            });
        var service = CreateService(planRepository: planRepository, itemRepository: itemRepository);

        var card = await service.GetCookingComponentCardAsync(21, 501);

        card.PlanItemId.Should().Be(21);
        card.RecipeComponentVersionId.Should().Be(501);
        card.PlannedQuantity.Should().Be(7);
        card.Ingredients.Should().ContainSingle().Which.Should().Match<CookingComponentIngredientDto>(ingredient =>
            ingredient.IngredientName == "Kurczak" &&
            ingredient.WeightPerServing == 180m &&
            ingredient.TotalWeight == 1260m);
        card.InstructionSections.Should().ContainSingle().Which.Steps.Should().ContainSingle().Which.Should()
            .Match<CookingComponentInstructionStepDto>(step =>
                step.StepId == 711 &&
                step.RequiresControl &&
                step.IsCritical);
    }

    [Fact]
    public async Task GetCookingCardAsync_ShouldExposeComponentSessionProgress()
    {
        var date = new DateOnly(2026, 6, 5);
        var snapshotItem = CreateSnapshotItem(date, 1001, 101, "Makaron standard", 501, 9001, 100m, 1.0m);
        snapshotItem.Components.Single().InstructionSections = new[]
        {
            new ComponentInstructionSectionDto
            {
                SectionId = 71,
                Title = "Kontrola",
                SortOrder = 1,
                Steps = new[]
                {
                    new ComponentInstructionStepDto { StepId = 711, StepText = "Kontrola temperatury", RequiresControl = true, IsCritical = true },
                    new ComponentInstructionStepDto { StepId = 712, StepText = "Wymieszaj", RequiresControl = false, IsCritical = false },
                },
            },
        };
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 4,
                M2SnapshotJson = JsonSerializer.Serialize(snapshotItem),
            });
        var cookingSessionService = new Mock<ICookingSessionService>();
        cookingSessionService
            .Setup(service => service.GetComponentSessionAsync(21, 501))
            .ReturnsAsync(new CookingComponentSessionDto
            {
                Status = "InProgress",
                StepChecksByStepId = new()
                {
                    [711] = new CookingStepCheckDto { StepId = 711, Status = "Checked" },
                },
            });
        var service = CreateService(
            itemRepository: itemRepository,
            cookingSessionService: cookingSessionService);

        var card = await service.GetCookingCardAsync(21);

        card.Components.Should().ContainSingle().Which.Should().Match<CookingCardComponentDto>(component =>
            component.RecipeComponentVersionId == 501 &&
            component.SessionStatus == "InProgress" &&
            component.TotalStepCount == 2 &&
            component.CheckedStepCount == 1 &&
            component.RequiredStepCount == 1 &&
            component.RequiredCheckedStepCount == 1 &&
            component.StatusLabel == "w toku" &&
            component.ProgressPercent == 50 &&
            component.ControlMessage == "Kontrole temperatury i kroki krytyczne są odznaczone.");
    }

    [Fact]
    public async Task GetCookingCardAsync_ShouldExposeApprovalBlockers_WhenFefoOrComponentsAreIncomplete()
    {
        var date = new DateOnly(2026, 6, 5);
        var snapshotItem = CreateSnapshotItem(date, 1001, 101, "Makaron standard", 501, 9001, 100m, 1.0m);
        snapshotItem.Components.Single().InstructionSections = new[]
        {
            new ComponentInstructionSectionDto
            {
                SectionId = 71,
                Title = "Kontrola",
                SortOrder = 1,
                Steps = new[]
                {
                    new ComponentInstructionStepDto { StepId = 711, StepText = "Kontrola temperatury", RequiresControl = true, IsCritical = true },
                },
            },
        };
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 4,
                M2SnapshotJson = JsonSerializer.Serialize(snapshotItem),
            });
        var cookingSessionService = new Mock<ICookingSessionService>();
        cookingSessionService
            .Setup(service => service.GetComponentSessionAsync(21, 501))
            .ReturnsAsync(new CookingComponentSessionDto
            {
                Status = "NotStarted",
            });
        var service = CreateService(
            itemRepository: itemRepository,
            cookingSessionService: cookingSessionService);

        var card = await service.GetCookingCardAsync(21);

        card.CanApproveCooking.Should().BeFalse();
        card.ApprovalBlockers.Should().Contain(blocker => blocker.Contains("FEFO", StringComparison.OrdinalIgnoreCase));
        card.ApprovalBlockers.Should().Contain(blocker => blocker.Contains("Skladowa 501", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCookingCardAsync_ShouldAllowApproval_WhenFefoAndComponentsAreComplete()
    {
        var date = new DateOnly(2026, 6, 5);
        var snapshotItem = CreateSnapshotItem(date, 1001, 101, "Makaron standard", 501, 9001, 100m, 1.0m);
        snapshotItem.Components.Single().InstructionSections = new[]
        {
            new ComponentInstructionSectionDto
            {
                SectionId = 71,
                Title = "Kontrola",
                SortOrder = 1,
                Steps = new[]
                {
                    new ComponentInstructionStepDto { StepId = 711, StepText = "Kontrola temperatury", RequiresControl = true, IsCritical = true },
                },
            },
        };
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 4,
                FefoDeductedAt = DateTimeOffset.UtcNow,
                M2SnapshotJson = JsonSerializer.Serialize(snapshotItem),
            });
        var cookingSessionService = new Mock<ICookingSessionService>();
        cookingSessionService
            .Setup(service => service.GetComponentSessionAsync(21, 501))
            .ReturnsAsync(new CookingComponentSessionDto
            {
                Status = "Completed",
                StepChecksByStepId = new()
                {
                    [711] = new CookingStepCheckDto { StepId = 711, Status = "Checked" },
                },
            });
        var service = CreateService(
            itemRepository: itemRepository,
            cookingSessionService: cookingSessionService);

        var card = await service.GetCookingCardAsync(21);

        card.CanApproveCooking.Should().BeTrue();
        card.ApprovalBlockers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetM2PlanOverviewAsync_ShouldShowProductionSnapshotStatusAndUnacknowledgedAlerts()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        snapshot.Alerts = new[]
        {
            new PlanChangeAlertDto
            {
                Id = 44,
                PlanDate = date,
                DietMenuPlanId = snapshot.DietMenuPlanId,
                AlertType = "PlanChanged",
                Severity = "Warning",
                Message = "Plan changed after publication",
                RequiresAcknowledgement = true,
                CreatedAt = DateTimeOffset.UtcNow,
            },
        };

        var planRepository = new Mock<IProductionPlanRepository>();
        planRepository
            .Setup(repository => repository.GetByDateAsync(date))
            .ReturnsAsync(new ProductionPlan { Id = 700, ProductionDate = date });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(700))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 21,
                    ProductionPlanId = 700,
                    MealId = 10,
                    DietVariantId = 1,
                    DietMenuPlanItemId = 1001,
                    PlannedQuantity = 3,
                    M2SnapshotJson = JsonSerializer.Serialize(snapshot.Items[0]),
                },
            });
        var dietProvider = new Mock<IDietDataProvider>();
        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);

        var service = CreateService(planRepository: planRepository, dietProvider: dietProvider);

        var overview = await service.GetM2PlanOverviewAsync(date, 1);

        overview.TotalDays.Should().Be(1);
        overview.UnacknowledgedAlertCount.Should().Be(1);
        overview.Days.Should().ContainSingle().Which.Should().Match<M2PlanOverviewDayDto>(day =>
            day.PlanDate == date &&
            day.IsPublished &&
            day.ProductionPlanId == 700 &&
            day.SnapshotItemCount == 1 &&
            day.MissingSnapshotItemCount == 1 &&
            day.UnacknowledgedAlertCount == 1);
        overview.Days.Single().Items.Should().Contain(item =>
            item.DietMenuPlanItemId == 1001 &&
            item.HasProductionSnapshot &&
            item.ProductionPlanItemId == 21 &&
            item.PlannedQuantity == 3);
    }

    [Fact]
    public async Task GetM2PlanOverviewAsync_ShouldMarkProductionSnapshotAsStale_WhenHashDiffersFromFreshM2Snapshot()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        var planRepository = new Mock<IProductionPlanRepository>();
        planRepository
            .Setup(repository => repository.GetByDateAsync(date))
            .ReturnsAsync(new ProductionPlan { Id = 700, ProductionDate = date });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(700))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 21,
                    ProductionPlanId = 700,
                    MealId = 10,
                    DietVariantId = 1,
                    DietMenuPlanItemId = 1001,
                    PlannedQuantity = 3,
                    M2SnapshotJson = JsonSerializer.Serialize(snapshot.Items[0]),
                    M2SnapshotHash = "stary-hash",
                },
            });
        var dietProvider = new Mock<IDietDataProvider>();
        dietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date))
            .ReturnsAsync(snapshot);

        var service = CreateService(planRepository: planRepository, dietProvider: dietProvider);

        var overview = await service.GetM2PlanOverviewAsync(date, 1);

        var item = overview.Days.Single().Items.Single(i => i.DietMenuPlanItemId == 1001);
        item.FreshSnapshotHash.Should().NotBeNullOrWhiteSpace();
        item.SnapshotHash.Should().Be("stary-hash");
        item.IsProductionSnapshotCurrent.Should().BeFalse();
        item.NeedsRefresh.Should().BeTrue();
        item.CanRefresh.Should().BeTrue();
        item.RefreshBlockers.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshProductionPlanFromM2Async_ShouldCreatePlan_WhenProductionPlanDoesNotExist()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        var orderProvider = new Mock<IOrderDataProvider>();
        var (generator, planRepository) = CreatePlanGenerator(snapshot, orderProvider);
        SetupDeliveries(
            orderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, 1001, "lunch"),
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 102, 1002, "lunch"));
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<ProductionPlanItem>()))
            .ReturnsAsync(1);
        var service = CreateService(
            planRepository: planRepository,
            itemRepository: itemRepository,
            dietProvider: generator.DietProvider,
            orderProvider: orderProvider,
            planGenerator: generator.Instance);

        var result = await service.RefreshProductionPlanFromM2Async(date, "admin");

        result.Status.Should().Be("Created");
        result.ProductionPlanId.Should().Be(77);
        result.ItemCount.Should().Be(2);
        result.Blockers.Should().BeEmpty();
        itemRepository.Verify(repository => repository.InsertAsync(It.IsAny<ProductionPlanItem>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshProductionPlanFromM2Async_ShouldReplaceItems_WhenExistingPlanHasNotStarted()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        var orderProvider = new Mock<IOrderDataProvider>();
        var (generator, planRepository) = CreatePlanGenerator(snapshot, orderProvider);
        planRepository
            .Setup(repository => repository.GetByDateAsync(date))
            .ReturnsAsync(new ProductionPlan { Id = 700, ProductionDate = date });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(700))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 21,
                    ProductionPlanId = 700,
                    MealId = 10,
                    DietVariantId = 1,
                    DietMenuPlanItemId = 1001,
                    PlannedQuantity = 1,
                    Status = ProductionItemStatus.Planned,
                },
            });
        SetupDeliveries(
            orderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, 1001, "lunch"),
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 102, 1002, "lunch"));
        var service = CreateService(
            planRepository: planRepository,
            dietProvider: generator.DietProvider,
            orderProvider: orderProvider,
            planGenerator: generator.Instance);

        var result = await service.RefreshProductionPlanFromM2Async(date, "admin");

        result.Status.Should().Be("Refreshed");
        result.ProductionPlanId.Should().Be(700);
        result.ItemCount.Should().Be(2);
        planRepository.Verify(repository => repository.ReplacePlanItemsAsync(
            700,
            It.Is<IReadOnlyList<ProductionPlanItem>>(items =>
                items.Count == 2 &&
                items.All(item => item.ProductionPlanId == 700) &&
                items.All(item => !string.IsNullOrWhiteSpace(item.M2SnapshotHash))),
            "admin"), Times.Once);
    }

    [Fact]
    public async Task RefreshProductionPlanFromM2Async_ShouldBlock_WhenExistingPlanHasStarted()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        var orderProvider = new Mock<IOrderDataProvider>();
        var (generator, planRepository) = CreatePlanGenerator(snapshot, orderProvider);
        planRepository
            .Setup(repository => repository.GetByDateAsync(date))
            .ReturnsAsync(new ProductionPlan { Id = 700, ProductionDate = date });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(700))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 21,
                    ProductionPlanId = 700,
                    MealId = 10,
                    DietVariantId = 1,
                    DietMenuPlanItemId = 1001,
                    PlannedQuantity = 1,
                    Status = ProductionItemStatus.Cooking,
                    FefoDeductedAt = DateTimeOffset.UtcNow,
                },
            });
        var service = CreateService(
            planRepository: planRepository,
            dietProvider: generator.DietProvider,
            orderProvider: orderProvider,
            planGenerator: generator.Instance);

        var result = await service.RefreshProductionPlanFromM2Async(date, "admin");

        result.Status.Should().Be("Blocked");
        result.Blockers.Should().Contain(blocker => blocker.Contains("FEFO", StringComparison.OrdinalIgnoreCase));
        result.Blockers.Should().Contain(blocker => blocker.Contains("Cooking", StringComparison.OrdinalIgnoreCase));
        planRepository.Verify(repository => repository.ReplacePlanItemsAsync(
            It.IsAny<int>(),
            It.IsAny<IReadOnlyList<ProductionPlanItem>>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshProductionPlansFromM2Async_ShouldSummarizeRangeResults()
    {
        var date = new DateOnly(2026, 6, 8);
        var snapshot = CreateSnapshot(date);
        var orderProvider = new Mock<IOrderDataProvider>();
        var (generator, planRepository) = CreatePlanGenerator(snapshot, orderProvider);
        planRepository
            .Setup(repository => repository.GetByDateAsync(date))
            .ReturnsAsync((ProductionPlan?)null);
        planRepository
            .Setup(repository => repository.GetByDateAsync(date.AddDays(1)))
            .ReturnsAsync(new ProductionPlan { Id = 701, ProductionDate = date.AddDays(1) });
        planRepository
            .Setup(repository => repository.GetPlanItemsAsync(701))
            .ReturnsAsync(new[]
            {
                new ProductionPlanItem
                {
                    Id = 22,
                    ProductionPlanId = 701,
                    MealId = 10,
                    DietVariantId = 1,
                    Status = ProductionItemStatus.Cooked,
                    CookedQuantity = 1,
                },
            });
        generator.DietProvider
            .Setup(provider => provider.GetPublishedPlanSnapshotAsync(date.AddDays(1)))
            .ReturnsAsync(CreateSnapshot(date.AddDays(1)));
        SetupDeliveries(
            orderProvider,
            date,
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, 1001, "lunch"));
        SetupDeliveries(
            orderProvider,
            date.AddDays(1),
            new OrderItemInfo(1, "Dieta", 1, "2000", 2000, 10, 101, 1001, "lunch"));
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<ProductionPlanItem>()))
            .ReturnsAsync(1);
        var service = CreateService(
            planRepository: planRepository,
            itemRepository: itemRepository,
            dietProvider: generator.DietProvider,
            orderProvider: orderProvider,
            planGenerator: generator.Instance);

        var result = await service.RefreshProductionPlansFromM2Async(date, 2, "admin");

        result.CreatedCount.Should().Be(1);
        result.BlockedCount.Should().Be(1);
        result.Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcknowledgePlanAlertAsync_ShouldPersistAcknowledgement()
    {
        var alertRepository = new Mock<IRepository<PlanChangeAlert>>();
        alertRepository
            .Setup(repository => repository.GetByIdAsync(44))
            .ReturnsAsync(new PlanChangeAlert
            {
                Id = 44,
                PlanDate = new DateOnly(2026, 6, 8),
                AlertType = "PlanChanged",
                Severity = "Warning",
                Message = "Plan changed",
                RequiresAcknowledgement = true,
            });
        var service = CreateService(alertRepository: alertRepository);

        await service.AcknowledgePlanAlertAsync(44, "kitchen-manager");

        alertRepository.Verify(repository => repository.UpdateAsync(It.Is<PlanChangeAlert>(alert =>
            alert.Id == 44 &&
            alert.AcknowledgedAt.HasValue &&
            alert.AcknowledgedBy == "kitchen-manager")), Times.Once);
    }

    [Fact]
    public async Task ApproveCookingAsync_ShouldRequireManagerApproval_WhenCookedQuantityDiffersFromPlan()
    {
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                MealId = 10,
                MealName = "Makaron standard",
                DietVariantId = 1,
                PlannedQuantity = 10,
                FefoDeductedAt = DateTimeOffset.UtcNow,
                PackagingDeductedAt = DateTimeOffset.UtcNow,
                M2SnapshotJson = JsonSerializer.Serialize(CreateSnapshotItem(
                    new DateOnly(2026, 6, 5),
                    1001,
                    101,
                    "Makaron standard",
                    501,
                    9001,
                    100m,
                    1m)),
            });
        var adjustmentRepository = new Mock<IProductionAdjustmentApprovalRepository>();
        adjustmentRepository
            .Setup(repository => repository.GetLatestApprovedAsync(
                21,
                It.IsAny<IReadOnlyCollection<string>>(),
                8m))
            .ReturnsAsync((ProductionAdjustmentApproval?)null);
        var service = CreateService(
            itemRepository: itemRepository,
            adjustmentRepository: adjustmentRepository);

        var act = () => service.ApproveCookingAsync(21, 8m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*akceptacji managera*");
        itemRepository.Verify(repository => repository.UpdateAsync(It.IsAny<ProductionPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task ApproveCookingAsync_ShouldApplyApprovedAdjustment_AndMarkItApplied()
    {
        var item = new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            MealId = 10,
            MealName = "Makaron standard",
            DietVariantId = 1,
            PlannedQuantity = 10,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            PackagingDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = JsonSerializer.Serialize(CreateSnapshotItem(
                new DateOnly(2026, 6, 5),
                1001,
                101,
                "Makaron standard",
                501,
                9001,
                100m,
                1m)),
        };
        var approval = new ProductionAdjustmentApproval
        {
            Id = 90,
            ProductionPlanItemId = 21,
            AdjustmentType = "CookedQuantity",
            Status = "Approved",
            PlannedValue = 10m,
            RequestedValue = 8m,
            Unit = "portion",
            Reason = "Niedobor po gotowaniu",
            RequestedBy = "Chef",
            RequestedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ApprovedBy = "Manager",
            ApprovedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(item);
        var adjustmentRepository = new Mock<IProductionAdjustmentApprovalRepository>();
        adjustmentRepository
            .Setup(repository => repository.GetLatestApprovedAsync(
                21,
                It.IsAny<IReadOnlyCollection<string>>(),
                8m))
            .ReturnsAsync(approval);
        var service = CreateService(
            itemRepository: itemRepository,
            adjustmentRepository: adjustmentRepository);

        await service.ApproveCookingAsync(21, 8m);

        item.CookedQuantity.Should().Be(8);
        approval.Status.Should().Be("Applied");
        approval.AppliedAt.Should().NotBeNull();
        adjustmentRepository.Verify(repository => repository.UpdateAsync(approval), Times.Once);
    }

    [Fact]
    public async Task ApproveCookingAsync_ShouldNotUsePackagingApproval_ForCookedQuantity()
    {
        var item = new ProductionPlanItem
        {
            Id = 21,
            ProductionPlanId = 5,
            MealId = 10,
            MealName = "Makaron standard",
            DietVariantId = 1,
            PlannedQuantity = 10,
            FefoDeductedAt = DateTimeOffset.UtcNow,
            PackagingDeductedAt = DateTimeOffset.UtcNow,
            M2SnapshotJson = JsonSerializer.Serialize(CreateSnapshotItem(
                new DateOnly(2026, 6, 5),
                1001,
                101,
                "Makaron standard",
                501,
                9001,
                100m,
                1m)),
        };
        var packagingApproval = new ProductionAdjustmentApproval
        {
            Id = 91,
            ProductionPlanItemId = 21,
            AdjustmentType = "PackagingQuantity",
            Status = "Approved",
            PlannedValue = 10m,
            RequestedValue = 8m,
            Unit = "container",
            Reason = "Mniej pojemnikow",
            RequestedBy = "Chef",
            RequestedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ApprovedBy = "Manager",
            ApprovedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(item);
        var adjustmentRepository = new Mock<IProductionAdjustmentApprovalRepository>();
        adjustmentRepository
            .Setup(repository => repository.GetLatestApprovedAsync(
                21,
                It.Is<IReadOnlyCollection<string>>(types => types.Contains("PackagingQuantity")),
                8m))
            .ReturnsAsync(packagingApproval);
        var service = CreateService(
            itemRepository: itemRepository,
            adjustmentRepository: adjustmentRepository);

        var act = () => service.ApproveCookingAsync(21, 8m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*akceptacji managera*");
        itemRepository.Verify(repository => repository.UpdateAsync(It.IsAny<ProductionPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task RequestProductionAdjustmentApprovalAsync_ShouldPersistPendingAuditEntry()
    {
        var itemRepository = new Mock<IRepository<ProductionPlanItem>>();
        itemRepository
            .Setup(repository => repository.GetByIdAsync(21))
            .ReturnsAsync(new ProductionPlanItem
            {
                Id = 21,
                ProductionPlanId = 5,
                PlannedQuantity = 10,
            });
        ProductionAdjustmentApproval? inserted = null;
        var adjustmentRepository = new Mock<IProductionAdjustmentApprovalRepository>();
        adjustmentRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<ProductionAdjustmentApproval>()))
            .Callback<ProductionAdjustmentApproval>(approval => inserted = approval)
            .ReturnsAsync(90);
        var service = CreateService(
            itemRepository: itemRepository,
            adjustmentRepository: adjustmentRepository);

        var result = await service.RequestProductionAdjustmentApprovalAsync(new ProductionAdjustmentApprovalRequestDto
        {
            ProductionPlanItemId = 21,
            AdjustmentType = "CookedQuantity",
            RequestedValue = 8m,
            Reason = "Niedobor po gotowaniu",
            RequestedBy = "Chef",
        });

        result.Id.Should().Be(90);
        result.Status.Should().Be("Pending");
        inserted.Should().NotBeNull();
        inserted!.PlannedValue.Should().Be(10m);
        inserted.RequestedValue.Should().Be(8m);
        inserted.Reason.Should().Be("Niedobor po gotowaniu");
    }

    private static ProductionService CreateService(
        Mock<IProductionPlanRepository>? planRepository = null,
        Mock<IRepository<ProductionPlanItem>>? itemRepository = null,
        Mock<IDietDataProvider>? dietProvider = null,
        Mock<IRepository<PlanChangeAlert>>? alertRepository = null,
        Mock<IProductionAdjustmentApprovalRepository>? adjustmentRepository = null,
        Mock<ICookingSessionService>? cookingSessionService = null,
        Mock<IOrderDataProvider>? orderProvider = null,
        ProductionPlanGenerator? planGenerator = null,
        FefoService? fefoService = null)
    {
        planRepository ??= new Mock<IProductionPlanRepository>();
        itemRepository ??= new Mock<IRepository<ProductionPlanItem>>();
        dietProvider ??= new Mock<IDietDataProvider>();
        alertRepository ??= new Mock<IRepository<PlanChangeAlert>>();
        adjustmentRepository ??= new Mock<IProductionAdjustmentApprovalRepository>();
        cookingSessionService ??= new Mock<ICookingSessionService>();

        orderProvider ??= new Mock<IOrderDataProvider>();
        planGenerator ??= new ProductionPlanGenerator(
            orderProvider.Object,
            dietProvider.Object,
            new FoodCostCalculator(dietProvider.Object),
            planRepository.Object);
        fefoService ??= new FefoService(
            Mock.Of<IBatchRepository>(),
            Mock.Of<IWarehouseCommandRepository>());

        return new ProductionService(
            planGenerator,
            planRepository.Object,
            itemRepository.Object,
            dietProvider.Object,
            Mock.Of<IPackingService>(),
            fefoService,
            alertRepository.Object,
            adjustmentRepository.Object,
            cookingSessionService.Object,
            Mock.Of<IMapper>(),
            Mock.Of<ILogger<ProductionService>>());
    }

    private static (GeneratorHarness generator, Mock<IProductionPlanRepository> planRepository) CreatePlanGenerator(
        PublishedDietPlanSnapshotDto snapshot,
        Mock<IOrderDataProvider>? orderProvider = null)
    {
        orderProvider ??= new Mock<IOrderDataProvider>();
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

        return (new GeneratorHarness(generator, orderProvider, dietProvider), planRepository);
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
        Mock<IOrderDataProvider> OrderProvider,
        Mock<IDietDataProvider> DietProvider);
}
