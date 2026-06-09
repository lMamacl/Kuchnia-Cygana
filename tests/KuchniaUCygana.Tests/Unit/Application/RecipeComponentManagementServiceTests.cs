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
    public async Task SearchAsync_NormalizesFiltersAndMapsPagedResult()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        RecipeComponentSearchQuery? capturedQuery = null;
        repository
            .Setup(r => r.SearchComponentsAsync(It.IsAny<RecipeComponentSearchQuery>()))
            .Callback<RecipeComponentSearchQuery>(query => capturedQuery = query)
            .ReturnsAsync(new RecipeComponentSearchResult
            {
                TotalCount = 1,
                Items =
                [
                    new RecipeComponentListRow
                    {
                        Id = 3,
                        CategoryId = 4,
                        CategoryName = "Sosy",
                        Name = "Sos cytrynowy",
                        LatestVersionId = 10,
                        LatestVersionNumber = 2,
                        LatestVersionStatus = "Draft",
                        HasPublicationGaps = true,
                        PublicationGapCount = 2,
                    },
                ],
            });
        var service = CreateService(repository);

        var result = await service.SearchAsync(new RecipeComponentSearchFilterDto
        {
            Query = "  sos  ",
            CategoryId = 4,
            VersionStatus = "Draft",
            AllergenId = 2,
            MissingPublicationData = true,
            Page = 0,
            PageSize = 500,
        });

        capturedQuery.Should().NotBeNull();
        capturedQuery!.Query.Should().Be("sos");
        capturedQuery.CategoryId.Should().Be(4);
        capturedQuery.VersionStatus.Should().Be("Draft");
        capturedQuery.AllergenId.Should().Be(2);
        capturedQuery.MissingPublicationData.Should().BeTrue();
        capturedQuery.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(100);
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.HasPublicationGaps && item.PublicationGapCount == 2);
    }

    [Fact]
    public async Task SearchPublishedVersionOptionsAsync_UsesRepositoryLookupWithLimit()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository
            .Setup(r => r.SearchPublishedVersionOptionsAsync("sos", 20))
            .ReturnsAsync(new[]
            {
                new RecipeComponentVersionOptionRow
                {
                    RecipeComponentId = 3,
                    RecipeComponentVersionId = 10,
                    ComponentName = "Sos",
                    VersionNumber = 2,
                },
            });
        var service = CreateService(repository);

        var result = await service.SearchPublishedVersionOptionsAsync("  sos  ", 500);

        repository.Verify(r => r.SearchPublishedVersionOptionsAsync("sos", 20), Times.Once);
        result.Should().ContainSingle(item => item.RecipeComponentVersionId == 10 && item.ComponentName == "Sos");
    }

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
        repository.Setup(r => r.GetVersionInstructionSectionsAsync(10)).ReturnsAsync(Array.Empty<RecipeComponentInstructionSectionRow>());
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
        repository.Setup(r => r.GetVersionInstructionSectionsAsync(10)).ReturnsAsync(new[]
        {
            new RecipeComponentInstructionSectionRow
            {
                Id = 1,
                RecipeComponentVersionId = 10,
                Title = "Przygotowanie",
                SortOrder = 1,
                Steps = new List<RecipeComponentInstructionStepRow>
                {
                    new()
                    {
                        Id = 1,
                        RecipeComponentInstructionSectionId = 1,
                        StepText = "Wymieszaj skladniki.",
                        SortOrder = 1,
                    },
                },
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

    [Fact]
    public async Task UpdateVersionAsync_RequiresReason_WhenNutritionSourceIsOverride()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(CreateCompleteDraft());
        var service = CreateService(repository);

        var act = async () => await service.UpdateVersionAsync(10, new UpdateRecipeComponentVersionRequest
        {
            NutritionSource = "Override",
            YieldQuantity = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Override wymaga powodu*");
        repository.Verify(r => r.UpdateDraftVersionAsync(It.IsAny<KuchniaUCygana.Domain.Entities.Menu.RecipeComponentVersion>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVersionAsync_RequiresReason_WhenAllergenApprovalIsOverride()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(CreateCompleteDraft());
        var service = CreateService(repository);

        var act = async () => await service.UpdateVersionAsync(10, new UpdateRecipeComponentVersionRequest
        {
            NutritionSource = "Manual",
            AllergensApproved = true,
            AllergenApprovalSource = "Override",
            YieldQuantity = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*alergenow poza danymi skladnikow wymaga powodu*");
        repository.Verify(r => r.UpdateDraftVersionAsync(It.IsAny<KuchniaUCygana.Domain.Entities.Menu.RecipeComponentVersion>()), Times.Never);
    }

    [Fact]
    public async Task GetVersionAsync_BuildsWeightAndNutritionComparison()
    {
        var repository = new Mock<IRecipeComponentRepository>();
        repository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(new RecipeComponentVersionRow
        {
            Id = 10,
            RecipeComponentId = 1,
            ComponentName = "Sos",
            VersionNumber = 1,
            Status = "Draft",
            YieldQuantity = 2m,
            YieldUnit = "portion",
            RawWeightGrams = 120m,
            CaloriesPer100g = 20m,
            ProteinPer100g = 1m,
            CarbohydratesPer100g = 2m,
            FatPer100g = 3m,
            FiberPer100g = 0m,
        });
        repository.Setup(r => r.GetVersionIngredientsAsync(10)).ReturnsAsync(new[]
        {
            new RecipeComponentIngredientRow
            {
                Id = 1,
                RecipeComponentVersionId = 10,
                IngredientId = 100,
                IngredientName = "Jogurt",
                WarehouseCategoryId = 3,
                WeightInGrams = 100m,
                YieldFactor = 0.8m,
                CaloriesPer100g = 60m,
                ProteinPer100g = 4m,
                CarbohydratesPer100g = 5m,
                FatPer100g = 3m,
                FiberPer100g = 0m,
            },
            new RecipeComponentIngredientRow
            {
                Id = 2,
                RecipeComponentVersionId = 10,
                IngredientId = 101,
                IngredientName = "Cytryna",
                WarehouseCategoryId = 3,
                WeightInGrams = 20m,
                YieldFactor = 1m,
                CaloriesPer100g = 30m,
                ProteinPer100g = 1m,
                CarbohydratesPer100g = 9m,
                FatPer100g = 0m,
                FiberPer100g = 2m,
            },
        });
        repository.Setup(r => r.GetVersionPackagingAsync(10)).ReturnsAsync(Array.Empty<PackagingRequirementRow>());
        repository.Setup(r => r.GetVersionInstructionSectionsAsync(10)).ReturnsAsync(Array.Empty<RecipeComponentInstructionSectionRow>());
        var service = CreateService(repository);

        var result = await service.GetVersionAsync(10);

        result.Should().NotBeNull();
        result!.Comparison.IngredientWeightGrams.Should().Be(120m);
        result.Comparison.IngredientGrossWeightGrams.Should().Be(145m);
        result.Comparison.ReferenceWeightSource.Should().Be("Raw");
        result.Comparison.CalculatedNutritionPer100g.Should().NotBeNull();
        result.Comparison.CalculatedNutritionPer100g!.CaloriesPer100g.Should().BeApproximately(55m, 0.0001m);
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
            AllergensApproved = true,
        };
    }
}
