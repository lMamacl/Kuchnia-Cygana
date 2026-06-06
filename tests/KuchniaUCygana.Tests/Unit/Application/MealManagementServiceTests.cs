using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class MealManagementServiceTests
{
    [Fact]
    public async Task PublishMealAsync_PublishesThroughCalculator_WhenResultIsComplete()
    {
        var mealRepository = new Mock<IMealRepository>();
        mealRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new Meal
        {
            Id = 5,
            Name = "Lunch testowy",
            Status = MealStatus.Draft,
        });
        var componentRepository = new Mock<IRecipeComponentRepository>();
        SetupCompleteBaseMeal(componentRepository);
        var service = CreateService(mealRepository: mealRepository, componentRepository: componentRepository);

        await service.PublishMealAsync(5);

        mealRepository.Verify(r => r.UpdateAsync(It.Is<Meal>(meal =>
            meal.Status == MealStatus.Published
            && meal.CookedWeightGrams == 200m
            && meal.RawWeightGrams == 220m)), Times.Once);
    }

    [Fact]
    public async Task PublishMealAsync_RejectsIncompleteCalculatorResult()
    {
        var mealRepository = new Mock<IMealRepository>();
        mealRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new Meal
        {
            Id = 5,
            Name = "Lunch testowy",
            Status = MealStatus.Draft,
        });
        var componentRepository = new Mock<IRecipeComponentRepository>();
        componentRepository.Setup(r => r.GetMealComponentDetailsAsync(5))
            .ReturnsAsync(Array.Empty<MealRecipeComponentDetailsRow>());
        componentRepository.Setup(r => r.GetMealPackagingAsync(5))
            .ReturnsAsync(Array.Empty<PackagingRequirementRow>());
        var service = CreateService(mealRepository: mealRepository, componentRepository: componentRepository);

        var act = async () => await service.PublishMealAsync(5);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nie mozna opublikowac posilku*");
        mealRepository.Verify(r => r.UpdateAsync(It.IsAny<Meal>()), Times.Never);
    }

    [Fact]
    public async Task PublishMealVariantAsync_PersistsCalculatedNutritionAndAllergens()
    {
        var mealRepository = new Mock<IMealRepository>();
        mealRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new Meal
        {
            Id = 5,
            Name = "Lunch testowy",
        });
        var variantRepository = new Mock<IMealVariantRepository>();
        variantRepository.Setup(r => r.GetByIdAsync(22)).ReturnsAsync(new MealVariantRow
        {
            Id = 22,
            MealId = 5,
            Name = "High protein",
            VariantType = "HighProtein",
            Status = "Draft",
            NutritionSource = "Aggregated",
            AllergensApproved = true,
        });
        variantRepository.Setup(r => r.GetComponentsAsync(22)).ReturnsAsync(new[]
        {
            CreateCompleteVariantComponent(),
        });
        variantRepository.Setup(r => r.GetPackagingAsync(22))
            .ReturnsAsync(Array.Empty<PackagingRequirementRow>());
        variantRepository.Setup(r => r.GetAllergensAsync(22))
            .ReturnsAsync(Array.Empty<MealVariantAllergenRow>());
        var componentRepository = new Mock<IRecipeComponentRepository>();
        componentRepository.Setup(r => r.GetMealComponentDetailsAsync(5))
            .ReturnsAsync(Array.Empty<MealRecipeComponentDetailsRow>());
        componentRepository.Setup(r => r.GetMealPackagingAsync(5))
            .ReturnsAsync(Array.Empty<PackagingRequirementRow>());
        SetupCompleteVersion(componentRepository, 10);
        var service = CreateService(
            mealRepository: mealRepository,
            componentRepository: componentRepository,
            variantRepository: variantRepository);

        await service.PublishMealVariantAsync(22);

        variantRepository.Verify(r => r.UpdateAsync(It.Is<MealVariant>(variant =>
            variant.Id == 22
            && variant.Status == "Published"
            && variant.CookedWeightGrams == 200m
            && variant.CaloriesPer100g == 120m)), Times.Once);
        variantRepository.Verify(r => r.ReplaceAllergensAsync(
            22,
            It.Is<IReadOnlyList<MealVariantAllergenRow>>(allergens =>
                allergens.Count == 1 && allergens[0].AllergenId == 7)), Times.Once);
    }

    [Fact]
    public async Task UpdateMealVariantAsync_RequiresReason_WhenNutritionSourceIsOverride()
    {
        var mealRepository = new Mock<IMealRepository>();
        mealRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new Meal { Id = 5, Name = "Lunch" });
        var variantRepository = new Mock<IMealVariantRepository>();
        variantRepository.Setup(r => r.GetByIdAsync(22)).ReturnsAsync(new MealVariantRow
        {
            Id = 22,
            MealId = 5,
            Name = "High protein",
        });
        var service = CreateService(mealRepository: mealRepository, variantRepository: variantRepository);

        var act = async () => await service.UpdateMealVariantAsync(22, new UpdateMealVariantRequest
        {
            Name = "High protein",
            NutritionSource = "Override",
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Override wymaga powodu*");
        variantRepository.Verify(r => r.UpdateAsync(It.IsAny<MealVariant>()), Times.Never);
    }

    [Fact]
    public async Task SaveMealVariantComponentAsync_RejectsUnpublishedComponentVersion()
    {
        var variantRepository = new Mock<IMealVariantRepository>();
        variantRepository.Setup(r => r.GetByIdAsync(22)).ReturnsAsync(new MealVariantRow { Id = 22, MealId = 5 });
        var componentRepository = new Mock<IRecipeComponentRepository>();
        componentRepository.Setup(r => r.GetVersionAsync(10)).ReturnsAsync(new RecipeComponentVersionRow
        {
            Id = 10,
            Status = "Draft",
        });
        var service = CreateService(componentRepository: componentRepository, variantRepository: variantRepository);

        var act = async () => await service.SaveMealVariantComponentAsync(22, new SaveMealVariantComponentRequest
        {
            RecipeComponentVersionId = 10,
            QuantityPerServing = 1m,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*opublikowana wersje skladowej*");
        variantRepository.Verify(r => r.SaveComponentAsync(It.IsAny<MealVariantComponent>()), Times.Never);
    }

    private static MealManagementService CreateService(
        Mock<IMealRepository>? mealRepository = null,
        Mock<IRecipeComponentRepository>? componentRepository = null,
        Mock<IMealVariantRepository>? variantRepository = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.GetUserName()).Returns("test-user");
        var allergenRepository = new Mock<IMealAllergenRepository>();
        allergenRepository.Setup(r => r.GetDetailsByMealIdAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<MealAllergenRow>());

        return new MealManagementService(
            mealRepository?.Object ?? Mock.Of<IMealRepository>(),
            Mock.Of<IRecipeRepository>(),
            componentRepository?.Object ?? Mock.Of<IRecipeComponentRepository>(),
            variantRepository?.Object ?? Mock.Of<IMealVariantRepository>(),
            allergenRepository.Object,
            Mock.Of<IMealImageRepository>(),
            new MealVariantResultCalculator(),
            currentUser.Object,
            Mock.Of<IMapper>());
    }

    private static void SetupCompleteBaseMeal(Mock<IRecipeComponentRepository> componentRepository)
    {
        componentRepository.Setup(r => r.GetMealComponentDetailsAsync(5)).ReturnsAsync(new[]
        {
            new MealRecipeComponentDetailsRow
            {
                RecipeComponentId = 3,
                RecipeComponentVersionId = 10,
                ComponentName = "Kurczak",
                VersionNumber = 1,
                VersionStatus = "Published",
                QuantityPerServing = 1m,
                Unit = "portion",
                YieldQuantity = 1m,
                YieldUnit = "portion",
                RawWeightGrams = 220m,
                CookedWeightGrams = 200m,
                CaloriesPer100g = 120m,
                ProteinPer100g = 20m,
                CarbohydratesPer100g = 5m,
                FatPer100g = 4m,
                FiberPer100g = 1m,
                AllergensApproved = true,
                IngredientId = 100,
                IngredientName = "Kurczak",
                WarehouseCategoryId = 4,
                WeightInGrams = 220m,
                YieldFactor = 1m,
            },
        });
        componentRepository.Setup(r => r.GetMealPackagingAsync(5))
            .ReturnsAsync(Array.Empty<PackagingRequirementRow>());
        SetupCompleteVersion(componentRepository, 10);
    }

    private static void SetupCompleteVersion(Mock<IRecipeComponentRepository> componentRepository, int versionId)
    {
        componentRepository.Setup(r => r.GetVersionIngredientsAsync(versionId)).ReturnsAsync(new[]
        {
            new RecipeComponentIngredientRow
            {
                RecipeComponentVersionId = versionId,
                IngredientId = 100,
                IngredientName = "Kurczak",
                WarehouseCategoryId = 4,
                WeightInGrams = 220m,
                YieldFactor = 1m,
            },
        });
        componentRepository.Setup(r => r.GetVersionPackagingAsync(versionId)).ReturnsAsync(new[]
        {
            new PackagingRequirementRow
            {
                OwnerType = "RecipeComponentVersion",
                RecipeComponentVersionId = versionId,
                ResourceName = "Pojemnik",
                WarehouseCategoryId = 8,
                Quantity = 1m,
                Unit = "pcs",
            },
        });
        componentRepository.Setup(r => r.GetVersionAllergensAsync(versionId)).ReturnsAsync(new[]
        {
            new RecipeComponentAllergenRow
            {
                AllergenId = 7,
                Name = "Seler",
                IsTrace = false,
                SourceType = "Ingredient",
                SourceName = "Kurczak",
            },
        });
    }

    private static MealVariantComponentRow CreateCompleteVariantComponent()
        => new()
        {
            Id = 1,
            MealVariantId = 22,
            RecipeComponentId = 3,
            RecipeComponentVersionId = 10,
            ComponentName = "Kurczak",
            VersionNumber = 1,
            VersionStatus = "Published",
            QuantityPerServing = 1m,
            Unit = "portion",
            YieldQuantity = 1m,
            YieldUnit = "portion",
            RawWeightGrams = 220m,
            CookedWeightGrams = 200m,
            CaloriesPer100g = 120m,
            ProteinPer100g = 20m,
            CarbohydratesPer100g = 5m,
            FatPer100g = 4m,
            FiberPer100g = 1m,
            AllergensApproved = true,
        };
}
