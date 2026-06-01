using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class RecipeComponentManagementServiceTests
{
    [Fact]
    public async Task PublishVersionAsync_RejectsIncompleteDraft()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(new RecipeComponentVersionRow
        {
            Id = 10,
            RecipeComponentId = 1,
            ComponentName = "Sos cytrynowy",
            VersionNumber = 1,
            Status = "Draft",
            YieldQuantity = 1m,
        });
        repository.Setup(r => r.GetVersionIngredientsAsync(10)).ReturnsAsync(Array.Empty<RecipeComponentIngredientRow>());
        repository.Setup(r => r.GetVersionPackagingAsync(10)).ReturnsAsync(Array.Empty<PackagingRequirementRow>());
        var service = CreateService(repository);

        var act = async () => await service.PublishVersionAsync(10);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nie mozna opublikowac wersji*");
        repository.Verify(r => r.PublishVersionAsync(It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task PublishVersionAsync_PublishesCompleteDraft()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(CreateCompleteDraft());
        repository.Setup(r => r.GetVersionIngredientsAsync(10)).ReturnsAsync(new[]
        {
            new RecipeComponentIngredientRow
            {
                Id = 1,
                RecipeComponentVersionId = 10,
                IngredientId = 100,
                IngredientName = "Cytryna",
                WarehouseCategoryId = 3,
                WeightInGrams = 20m,
                YieldFactor = 1m,
            },
        });
        repository.Setup(r => r.GetVersionPackagingAsync(10)).ReturnsAsync(new[]
        {
            new PackagingRequirementRow
            {
                Id = 1,
                OwnerType = "RecipeComponentVersion",
                RecipeComponentVersionId = 10,
                ResourceName = "Pojemnik sos",
                WarehouseCategoryId = 8,
                Quantity = 1m,
                Unit = "pcs",
            },
        });
        var service = CreateService(repository);

        await service.PublishVersionAsync(10);

        repository.Verify(r => r.PublishVersionAsync(10, "test-user"), Times.Once);
    }

    [Fact]
    public async Task AttachComponentToMealAsync_RejectsDraftVersion()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(CreateCompleteDraft());
        var service = CreateService(repository);

        var act = async () => await service.AttachComponentToMealAsync(new AttachComponentToMealRequest
        {
            MealId = 5,
            RecipeComponentVersionId = 10,
            QuantityPerServing = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tylko opublikowana wersje*");
        repository.Verify(r => r.AttachComponentToMealAsync(It.IsAny<KuchniaUCygana.Domain.Entities.Menu.MealRecipeComponent>()), Times.Never);
    }

    private static RecipeComponentManagementService CreateService(Mock<IRecipeComponentRepository> repository)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.GetUserName()).Returns("test-user");
        return new RecipeComponentManagementService(repository.Object, currentUser.Object);
    }

    private static RecipeComponentVersionRow CreateCompleteDraft()
    {
        return new RecipeComponentVersionRow
        {
            Id = 10,
            RecipeComponentId = 1,
            ComponentName = "Sos cytrynowy",
            VersionNumber = 1,
            Status = "Draft",
            YieldQuantity = 1m,
            CaloriesPer100g = 50m,
            ProteinPer100g = 1m,
            CarbohydratesPer100g = 8m,
            FatPer100g = 2m,
            FiberPer100g = 0.5m,
        };
    }
}
