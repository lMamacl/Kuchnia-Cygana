using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class DietMenuPlanManagementServiceTests
{
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
    public async Task PublishAsync_PublishesCompletePlan()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(new[]
        {
            CreateValidItem(),
        });
        var recipeEngine = new Mock<IRecipeEngine>();
        recipeEngine.Setup(r => r.ValidateRecipeAsync(5)).ReturnsAsync(true);
        var service = CreateService(repository, recipeEngine);

        await service.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = 10 });

        repository.Verify(r => r.PublishAsync(10, "test-user"), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_RejectsPlanItemWithoutLabelData()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        var item = CreateValidItem();
        item.HasNutrition = false;
        item.AllergenCount = 0;
        item.PackagingRequirementCount = 0;
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(new[]
        {
            item,
        });
        var recipeEngine = new Mock<IRecipeEngine>();
        recipeEngine.Setup(r => r.ValidateRecipeAsync(5)).ReturnsAsync(true);
        var service = CreateService(repository, recipeEngine);

        var act = async () => await service.PublishAsync(new PublishDietMenuPlanRequest { DietMenuPlanId = 10 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nutrition*alergenow*opakowania*");
        repository.Verify(r => r.PublishAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task CopyDayAsync_RejectsEmptySourceDay()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(CreateDraftPlan());
        repository.Setup(r => r.GetPlanItemsAsync(10)).ReturnsAsync(Array.Empty<DietMenuPlanItemRow>());
        var service = CreateService(repository);

        var act = async () => await service.CopyDayAsync(new CopyDietMenuDayRequest
        {
            SourcePlanId = 10,
            TargetDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1),
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pustego dnia*");
        repository.Verify(r => r.CopyDayAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_BlocksPublishedPlanAfterDMinusThree()
    {
        var repository = new Mock<IDietMenuPlanRepository>();
        repository.Setup(r => r.GetPlanByIdAsync(10)).ReturnsAsync(new DietMenuPlanDayRow
        {
            Id = 10,
            PlanDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1),
            Status = "Published",
        });
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
        repository.Verify(r => r.AddItemAsync(It.IsAny<KuchniaUCygana.Domain.Entities.Menu.DietMenuPlanItem>()), Times.Never);
    }

    private static DietMenuPlanManagementService CreateService(
        Mock<IDietMenuPlanRepository> repository,
        Mock<IRecipeEngine>? recipeEngine = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.GetUserName()).Returns("test-user");
        return new DietMenuPlanManagementService(
            repository.Object,
            recipeEngine?.Object ?? Mock.Of<IRecipeEngine>(),
            currentUser.Object);
    }

    private static DietMenuPlanDayRow CreateDraftPlan()
    {
        return new DietMenuPlanDayRow
        {
            Id = 10,
            PlanDate = DateOnly.FromDateTime(DateTime.Today).AddDays(7),
            Status = "Draft",
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
}
