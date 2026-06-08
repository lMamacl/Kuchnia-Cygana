using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class DietMenuPlanManagementServiceTests
{
    [Fact]
    public async Task GetWeekAsync_UsesAtLeastSevenDays()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        DateOnly capturedStart = default;
        DateOnly capturedEnd = default;
        repository
            .Setup(r => r.GetPlanSummariesAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Callback<DateOnly, DateOnly>((start, end) =>
            {
                capturedStart = start;
                capturedEnd = end;
            })
            .ReturnsAsync(Array.Empty<DietMenuPlanDaySummaryRow>());
        var service = CreateService(repository);
        var startDate = DateOnly.FromDateTime(DateTime.Today);

        var result = await service.GetWeekAsync(startDate, 3);

        result.DaysCount.Should().Be(7);
        result.Days.Should().HaveCount(7);
        capturedStart.Should().Be(startDate);
        capturedEnd.Should().Be(startDate.AddDays(6));
        repository.Verify(r => r.GetPlanItemsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_RejectsEmptyPlan()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(Array.Empty<DietMenuPlanItemRow>());
        var service = CreateService(repository);

        var act = async () => await service.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = 10 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plan dnia nie ma pozycji*");
        repository.Verify(r => r.PublishAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GetDayShellAsync_LoadsVariantSummariesWithoutPlanItems()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByDateAsync(It.IsAny<DateOnly>())).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanDietVariantSummariesAsync(10)).ReturnsAsync(new[]
        {
            new DietMenuPlanDietVariantSummaryRow
            {
                DietVariantId = 2,
                DietName = "Slim",
                VariantName = "1800 kcal",
                TargetCalories = 1800,
                ActiveItemCount = 5,
                QuickWarningCount = 1,
            },
        });
        var mealService = CreateDefaultMealService();
        var service = CreateService(repository, mealService);

        var result = await service.GetDayShellAsync(DateOnly.FromDateTime(DateTime.Today));

        result.Id.Should().Be(10);
        result.ActiveItemCount.Should().Be(5);
        result.QuickWarningCount.Should().Be(1);
        result.DietVariants.Should().ContainSingle(v => v.DietVariantId == 2);
        repository.Verify(r => r.GetPlanItemsAsync(It.IsAny<int>()), Times.Never);
        mealService.Verify(s => s.GetMealVariantResultsAsync(
            It.IsAny<IEnumerable<MealVariantResultKey>>(),
            It.IsAny<MealVariantResultCacheMode>()), Times.Never);
    }

    [Fact]
    public async Task GetDietVariantItemsAsync_LoadsOnlySelectedVariant_AndUsesCachePreferredBatch()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanDietVariantSummariesAsync(10)).ReturnsAsync(new[]
        {
            new DietMenuPlanDietVariantSummaryRow
            {
                DietVariantId = 2,
                DietName = "Slim",
                VariantName = "1800 kcal",
                TargetCalories = 1800,
                ActiveItemCount = 1,
            },
        });
        repository.Setup(r => r.GetPlanItemsAsync(10, 2)).ReturnsAsync(new[] { CreateValidItem() });
        var mealService = new Mock<IMealManagementService>();
        mealService
            .Setup(s => s.GetMealVariantResultsAsync(
                It.IsAny<IEnumerable<MealVariantResultKey>>(),
                MealVariantResultCacheMode.CachePreferred))
            .ReturnsAsync(BatchResult(CreateCompleteResult()));
        var service = CreateService(repository, mealService);

        var result = await service.GetDietVariantItemsAsync(10, 2);

        result.DietVariantId.Should().Be(2);
        result.Items.Should().ContainSingle();
        repository.Verify(r => r.GetPlanItemsAsync(10, 2), Times.Once);
        mealService.Verify(s => s.GetMealVariantResultsAsync(
            It.IsAny<IEnumerable<MealVariantResultKey>>(),
            MealVariantResultCacheMode.CachePreferred), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_PublishesWhenCalculatorResultIsComplete_EvenWhenSqlCountsAreMissing()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        var item = CreateValidItem();
        item.HasNutrition = false;
        item.AllergenCount = 0;
        item.PackagingRequirementCount = 0;
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(new[] { item });
        var mealService = new Mock<IMealManagementService>();
        mealService
            .Setup(s => s.GetMealVariantResultsAsync(
                It.IsAny<IEnumerable<MealVariantResultKey>>(),
                MealVariantResultCacheMode.Fresh))
            .ReturnsAsync(BatchResult(CreateCompleteResult()));
        var service = CreateService(repository, mealService);

        await service.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = 10 });

        repository.Verify(r => r.PublishAsync(10, "test-user"), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_RejectsPlanItemWhenCalculatorResultIsIncomplete()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(new[] { CreateValidItem() });
        var mealService = new Mock<IMealManagementService>();
        mealService
            .Setup(s => s.GetMealVariantResultsAsync(
                It.IsAny<IEnumerable<MealVariantResultKey>>(),
                MealVariantResultCacheMode.Fresh))
            .ReturnsAsync(BatchResult(new MealVariantResultDto
            {
                MealId = 5,
                MealName = "Lunch testowy",
                IsComplete = false,
                CompletenessStatus = "Incomplete",
                ValidationWarnings = ["brak opakowania produkcyjnego", "brak kompletnego nutrition"],
            }));
        var service = CreateService(repository, mealService);

        var act = async () => await service.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = 10 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*brak opakowania produkcyjnego*brak kompletnego nutrition*");
        repository.Verify(r => r.PublishAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_BlocksPublishedPlanAfterDMinusThree()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreatePublishedBlockedPlan());
        var service = CreateService(repository);

        var act = async () => await service.AddItemAsync(new AddDietMenuPlanItemRequest
        {
            DietMenuPlanId = 10,
            DietVariantId = 2,
            MealId = 5,
            MealSlot = "Lunch",
            ServingSizeMultiplier = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*D-3*");
        repository.Verify(r => r.AddItemAsync(It.IsAny<DietMenuPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateItemAsync_BlocksPublishedPlanAfterDMinusThree()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanItemAsync(1)).ReturnsAsync(CreateValidItem());
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreatePublishedBlockedPlan());
        var service = CreateService(repository);

        var act = async () => await service.UpdateItemAsync(new UpdateDietMenuPlanItemRequest
        {
            Id = 1,
            DietMenuPlanId = 10,
            DietVariantId = 2,
            MealId = 5,
            MealSlot = "Lunch",
            ServingSizeMultiplier = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*D-3*");
        repository.Verify(r => r.UpdateItemAsync(It.IsAny<DietMenuPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task DeleteItemAsync_BlocksPublishedPlanAfterDMinusThree()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanItemAsync(1)).ReturnsAsync(CreateValidItem());
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreatePublishedBlockedPlan());
        var service = CreateService(repository);

        var act = async () => await service.DeleteItemAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*D-3*");
        repository.Verify(r => r.SoftDeleteItemAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task CopyDayAsync_BlocksPublishedTargetPlanAfterDMinusThree()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(new[] { CreateValidItem() });
        repository.Setup(r => r.GetPlanByDateAsync(It.IsAny<DateOnly>())).ReturnsAsync(CreatePublishedBlockedPlan());
        var service = CreateService(repository);

        var act = async () => await service.CopyDayAsync(new CopyDietMenuDayRequest
        {
            SourcePlanId = 10,
            TargetDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1),
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*D-3*");
        repository.Verify(r => r.CopyDayAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_BlocksPublishedPlanAfterDMinusThree()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreatePublishedBlockedPlan());
        var service = CreateService(repository);

        var act = async () => await service.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = 10 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*D-3*");
        repository.Verify(r => r.PublishAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_RejectsMealVariantFromDifferentMeal()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        var mealService = new Mock<IMealManagementService>();
        mealService.Setup(s => s.GetMealVariantResultAsync(5, 99)).ReturnsAsync((MealVariantResultDto?)null);
        var service = CreateService(repository, mealService);

        var act = async () => await service.AddItemAsync(new AddDietMenuPlanItemRequest
        {
            DietMenuPlanId = 10,
            DietVariantId = 2,
            MealId = 5,
            MealVariantId = 99,
            MealSlot = "Lunch",
            ServingSizeMultiplier = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Wariant dania nie nalezy*");
        repository.Verify(r => r.AddItemAsync(It.IsAny<DietMenuPlanItem>()), Times.Never);
    }

    private static DietMenuPlanManagementService CreateService(
        Mock<IDietMenuPlanRepository> repository,
        Mock<IMealManagementService>? mealService = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.GetUserName()).Returns("test-user");
        return new DietMenuPlanManagementService(
            repository.Object,
            mealService?.Object ?? CreateDefaultMealService().Object,
            currentUser.Object);
    }

    private static Mock<IMealManagementService> CreateDefaultMealService()
    {
        var mealService = new Mock<IMealManagementService>();
        mealService
            .Setup(s => s.GetMealVariantResultAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(CreateCompleteResult());
        mealService
            .Setup(s => s.GetMealVariantResultsAsync(
                It.IsAny<IEnumerable<MealVariantResultKey>>(),
                It.IsAny<MealVariantResultCacheMode>()))
            .ReturnsAsync((IEnumerable<MealVariantResultKey> keys, MealVariantResultCacheMode _) =>
                keys.ToDictionary(key => key, _ => (MealVariantResultDto?)CreateCompleteResult()));
        return mealService;
    }

    private static IReadOnlyDictionary<MealVariantResultKey, MealVariantResultDto?> BatchResult(
        MealVariantResultDto? result,
        int mealId = 5,
        int? mealVariantId = null)
        => new Dictionary<MealVariantResultKey, MealVariantResultDto?>
        {
            [new MealVariantResultKey(mealId, mealVariantId)] = result,
        };

    private static DietMenuPlanDayRow CreateDraftPlan()
    {
        return new DietMenuPlanDayRow
        {
            Id = 10,
            PlanDate = DateOnly.FromDateTime(DateTime.Today).AddDays(7),
            Status = "Draft",
        };
    }

    private static DietMenuPlanDayRow CreatePublishedBlockedPlan()
    {
        return new DietMenuPlanDayRow
        {
            Id = 10,
            PlanDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1),
            Status = "Published",
        };
    }

    private static DietMenuPlanItemRow CreateValidItem()
    {
        return new DietMenuPlanItemRow
        {
            Id = 1,
            DietMenuPlanId = 10,
            PlanDate = DateOnly.FromDateTime(DateTime.Today).AddDays(7),
            PlanStatus = "Draft",
            DietVariantId = 2,
            DietName = "Slim",
            VariantName = "1800 kcal",
            MealId = 5,
            MealName = "Lunch testowy",
            MealStatus = "Published",
            MealSlot = "Lunch",
            ServingSizeMultiplier = 1m,
            SortOrder = 3,
            ComponentCount = 1,
            LegacyRecipeCount = 0,
            HasNutrition = true,
            AllergenCount = 1,
            PackagingRequirementCount = 1,
            MissingWarehouseCategoryCount = 0,
        };
    }

    private static MealVariantResultDto CreateCompleteResult()
    {
        return new MealVariantResultDto
        {
            MealId = 5,
            MealName = "Lunch testowy",
            FinalWeightGrams = 350m,
            IsComplete = true,
            CompletenessStatus = "Complete",
            ValidationWarnings = Array.Empty<string>(),
            NutritionPer100g = new MealVariantNutritionDto
            {
                Calories = 120m,
                Protein = 20m,
                Carbohydrates = 5m,
                Fat = 4m,
                Fiber = 1m,
            },
            NutritionPerServing = new MealVariantNutritionDto
            {
                Calories = 420m,
                Protein = 70m,
                Carbohydrates = 17.5m,
                Fat = 14m,
                Fiber = 3.5m,
            },
            PackagingRequirements =
            [
                new MealVariantResultPackagingDto
                {
                    OwnerType = "Meal",
                    MealId = 5,
                    ResourceName = "Pojemnik",
                    WarehouseCategoryId = 8,
                    Quantity = 1m,
                },
            ],
        };
    }
}
