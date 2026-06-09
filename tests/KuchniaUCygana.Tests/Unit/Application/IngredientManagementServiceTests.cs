using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Mappings;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class IngredientManagementServiceTests
{
    [Fact]
    public async Task SearchAsync_NormalizesPagingAndMapsRows()
    {
        var repository = new Mock<IIngredientRepository>();
        IngredientSearchQuery? capturedQuery = null;
        repository
            .Setup(r => r.SearchAsync(It.IsAny<IngredientSearchQuery>()))
            .Callback<IngredientSearchQuery>(query => capturedQuery = query)
            .ReturnsAsync(new IngredientSearchResult
            {
                TotalCount = 1,
                Items =
                [
                    new IngredientListRow
                    {
                        Id = 7,
                        Name = "Pudelko lunch",
                        ResourceType = "Packaging",
                        Unit = "pcs",
                        WarehouseCategoryName = "Opakowania",
                        MissingWarehouseMapping = false,
                        CaloriesPer100g = 0m,
                        AllergenNames = "Gluten",
                        IsActive = true,
                    },
                ],
            });
        var service = CreateService(repository);

        var result = await service.SearchAsync(new IngredientSearchFilterDto
        {
            Search = "  pudelko  ",
            ResourceType = "Packaging",
            AllergenId = 4,
            MissingWarehouseMapping = true,
            IsActive = true,
            Page = 0,
            PageSize = 500,
        });

        capturedQuery.Should().NotBeNull();
        capturedQuery!.Search.Should().Be("pudelko");
        capturedQuery.ResourceType.Should().Be("Packaging");
        capturedQuery.AllergenId.Should().Be(4);
        capturedQuery.MissingWarehouseMapping.Should().BeTrue();
        capturedQuery.IsActive.Should().BeTrue();
        capturedQuery.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(100);
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.Id == 7 && item.ResourceType == "Packaging");
    }

    [Fact]
    public async Task SearchRecipeLookupAsync_UsesDedicatedRepositoryAndClampsPageSize()
    {
        var repository = new Mock<IIngredientRepository>();
        repository
            .Setup(r => r.SearchRecipeLookupAsync("sel", 1, 20))
            .ReturnsAsync(new[]
            {
                new IngredientListRow
                {
                    Id = 9,
                    Name = "Seler",
                    ResourceType = "Food",
                    Unit = "g",
                    IsActive = true,
                },
            });
        var service = CreateService(repository);

        var result = await service.SearchRecipeLookupAsync("  sel  ", 0, 200);

        repository.Verify(r => r.SearchRecipeLookupAsync("sel", 1, 20), Times.Once);
        result.Should().ContainSingle(item => item.Id == 9 && item.ResourceType == "Food");
    }

    [Fact]
    public async Task GetAsync_ReturnsNutritionAndIngredientAllergens()
    {
        var repository = new Mock<IIngredientRepository>();
        repository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(new Ingredient
        {
            Id = 10,
            Name = "Maka",
            ResourceType = "Food",
            Unit = "g",
            IsActive = true,
        });
        repository.Setup(r => r.GetIngredientAllergensAsync(10)).ReturnsAsync(new[]
        {
            new IngredientAllergenRow
            {
                IngredientId = 10,
                AllergenId = 2,
                Name = "Gluten",
                Code = "GLU",
                TraceAmount = true,
            },
        });

        var nutrition = new Mock<INutritionFactRepository>();
        nutrition.Setup(r => r.GetForIngredientAsync(10)).ReturnsAsync(new NutritionFact
        {
            Id = 11,
            IngredientId = 10,
            CaloriesPer100g = 340m,
            ProteinPer100g = 11m,
            CarbohydratesPer100g = 70m,
            FatPer100g = 1.5m,
            FiberPer100g = 2m,
        });
        var service = CreateService(repository, nutrition);

        var result = await service.GetAsync(10);

        result.Should().NotBeNull();
        result!.Nutrition.CaloriesPer100g.Should().Be(340m);
        result.Allergens.Should().ContainSingle(a => a.AllergenId == 2 && a.TraceAmount);
        result.SelectedAllergenIds.Should().ContainSingle().Which.Should().Be(2);
        result.TraceAllergenIds.Should().ContainSingle().Which.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_SavesNutritionAndAllergens()
    {
        var repository = new Mock<IIngredientRepository>();
        repository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(new Ingredient
        {
            Id = 10,
            Name = "Maka",
            ResourceType = "Food",
            Unit = "g",
            IsActive = true,
        });
        var nutrition = new Mock<INutritionFactRepository>();
        nutrition.Setup(r => r.GetForIngredientAsync(10)).ReturnsAsync((NutritionFact?)null);
        var service = CreateService(repository, nutrition);

        await service.UpdateAsync(new IngredientDto
        {
            Id = 10,
            Name = "Maka pszenna",
            ResourceType = "Food",
            Unit = "g",
            IsActive = true,
            Nutrition = new NutritionFactDto
            {
                CaloriesPer100g = 350m,
                ProteinPer100g = 12m,
            },
            SelectedAllergenIds = [2, 5],
            TraceAllergenIds = [5],
        });

        repository.Verify(r => r.UpdateAsync(It.Is<Ingredient>(ingredient =>
            ingredient.Id == 10
            && ingredient.Name == "Maka pszenna")), Times.Once);
        nutrition.Verify(r => r.InsertAsync(It.Is<NutritionFact>(fact =>
            fact.IngredientId == 10
            && fact.CaloriesPer100g == 350m
            && fact.ProteinPer100g == 12m)), Times.Once);
        repository.Verify(r => r.SaveIngredientAllergensAsync(
            10,
            It.Is<IReadOnlyList<IngredientAllergen>>(allergens =>
                allergens.Count == 2
                && allergens.Any(a => a.AllergenId == 2 && !a.TraceAmount)
                && allergens.Any(a => a.AllergenId == 5 && a.TraceAmount))), Times.Once);
    }

    private static IngredientManagementService CreateService(
        Mock<IIngredientRepository> repository,
        Mock<INutritionFactRepository>? nutrition = null)
    {
        var deletionGuard = new Mock<IIngredientDeletionGuard>();
        return new IngredientManagementService(
            repository.Object,
            (nutrition ?? new Mock<INutritionFactRepository>()).Object,
            deletionGuard.Object,
            Mapper());
    }

    private static IMapper Mapper()
    {
        return new MapperConfiguration(
            cfg => cfg.AddProfile<MenuProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
    }
}
