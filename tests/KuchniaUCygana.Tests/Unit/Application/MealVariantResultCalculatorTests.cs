using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Services.Menu;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class MealVariantResultCalculatorTests
{
    private readonly MealVariantResultCalculator calculator = new();

    [Fact]
    public void Calculate_UsesBaseComponents_WhenMealVariantIdIsMissing()
    {
        var result = this.calculator.Calculate(new MealVariantResultCalculationRequest
        {
            MealId = 10,
            MealName = "Lunch bazowy",
            NutritionSource = "Aggregated",
            AllergensApproved = true,
            BaseComponents =
            [
                CreateComponent(
                    versionId: 100,
                    name: "Ryż",
                    quantityPerServing: 1m,
                    unit: "portion",
                    yieldQuantity: 4m,
                    rawWeight: 800m,
                    cookedWeight: 600m,
                    ingredientWeight: 400m,
                    packagingQuantity: 4m),
            ],
            MealPackagingRequirements =
            [
                CreatePackaging("Meal", "Pudelko lunch", quantity: 1m),
            ],
        });

        result.IsComplete.Should().BeTrue();
        result.IsAggregated.Should().BeTrue();
        result.FinalRawWeightGrams.Should().Be(200m);
        result.FinalCookedWeightGrams.Should().Be(150m);
        result.FinalWeightGrams.Should().Be(150m);
        result.NutritionPer100g!.Calories.Should().Be(100m);
        result.NutritionPerServing!.Calories.Should().Be(150m);
        result.Ingredients.Should().ContainSingle();
        result.Ingredients[0].NetWeightInGrams.Should().Be(100m);
        result.Ingredients[0].GrossWeightInGrams.Should().Be(125m);
        result.PackagingRequirements.Sum(p => p.Quantity).Should().Be(2m);
        result.Components[0].ScaleFactor.Should().Be(0.25m);
    }

    [Fact]
    public void Calculate_UsesVariantComponents_WhenMealVariantIdIsProvided()
    {
        var result = this.calculator.Calculate(new MealVariantResultCalculationRequest
        {
            MealId = 10,
            MealName = "Lunch",
            MealVariantId = 55,
            MealVariantName = "High protein",
            VariantType = "HighProtein",
            NutritionSource = "Aggregated",
            AllergensApproved = true,
            BaseComponents =
            [
                CreateComponent(100, "Baza", 1m, "portion", 1m, 100m, 100m, 100m, 1m),
            ],
            VariantComponents =
            [
                CreateComponent(200, "Kurczak extra", 1m, "portion", 1m, 220m, 200m, 180m, 1m),
            ],
        });

        result.IsComplete.Should().BeTrue();
        result.MealVariantId.Should().Be(55);
        result.Components.Should().ContainSingle();
        result.Components[0].RecipeComponentVersionId.Should().Be(200);
        result.Components[0].ComponentName.Should().Be("Kurczak extra");
        result.FinalWeightGrams.Should().Be(200m);
        result.Ingredients.Should().ContainSingle(i => i.SourceRecipeComponentVersionIds.Contains(200));
        result.Ingredients.Should().NotContain(i => i.SourceRecipeComponentVersionIds.Contains(100));
    }

    [Fact]
    public void Calculate_GUnitTreatsQuantityAsDirectFinalGrams()
    {
        var result = this.calculator.Calculate(new MealVariantResultCalculationRequest
        {
            MealId = 10,
            MealName = "Lunch gramowy",
            NutritionSource = "Aggregated",
            AllergensApproved = true,
            BaseComponents =
            [
                CreateComponent(
                    versionId: 100,
                    name: "Sos",
                    quantityPerServing: 125m,
                    unit: "g",
                    yieldQuantity: 5m,
                    rawWeight: 700m,
                    cookedWeight: 500m,
                    ingredientWeight: 1000m,
                    packagingQuantity: 4m),
            ],
        });

        result.IsComplete.Should().BeTrue();
        result.Components[0].ScaleFactor.Should().Be(0.25m);
        result.FinalCookedWeightGrams.Should().Be(125m);
        result.FinalRawWeightGrams.Should().Be(175m);
        result.Ingredients[0].NetWeightInGrams.Should().Be(250m);
        result.PackagingRequirements[0].Quantity.Should().Be(1m);
    }

    [Fact]
    public void Calculate_UnsupportedUnitAddsWarningAndBlocksCompleteness()
    {
        var result = this.calculator.Calculate(new MealVariantResultCalculationRequest
        {
            MealId = 10,
            MealName = "Lunch",
            NutritionSource = "Aggregated",
            AllergensApproved = true,
            BaseComponents =
            [
                CreateComponent(100, "Zupa", 250m, "ml", 1m, 500m, 500m, 500m, 1m),
            ],
        });

        result.IsComplete.Should().BeFalse();
        result.ValidationWarnings.Should().Contain(warning => warning.Contains("nieobslugiwana jednostka"));
        result.ValidationWarnings.Should().Contain(warning => warning.Contains("brak finalnej gramatury"));
    }

    [Fact]
    public void Calculate_ManualOverrideRequiresReason()
    {
        var result = this.calculator.Calculate(new MealVariantResultCalculationRequest
        {
            MealId = 10,
            MealName = "Lunch",
            NutritionSource = "Override",
            ManualCookedWeightGrams = 300m,
            ManualNutritionPer100g = new MealVariantNutritionDto
            {
                Calories = 90m,
                Protein = 10m,
                Carbohydrates = 12m,
                Fat = 2m,
                Fiber = 1m,
            },
            AllergensApproved = true,
            BaseComponents =
            [
                CreateComponent(100, "Baza", 1m, "portion", 1m, 300m, 300m, 300m, 1m),
            ],
        });

        result.IsAggregated.Should().BeFalse();
        result.IsComplete.Should().BeFalse();
        result.NutritionPerServing!.Calories.Should().Be(270m);
        result.ValidationWarnings.Should().Contain("reczny override wyniku wymaga powodu");
    }

    private static MealVariantResultComponentInputDto CreateComponent(
        int versionId,
        string name,
        decimal quantityPerServing,
        string unit,
        decimal yieldQuantity,
        decimal rawWeight,
        decimal cookedWeight,
        decimal ingredientWeight,
        decimal packagingQuantity)
    {
        return new MealVariantResultComponentInputDto
        {
            RecipeComponentId = versionId / 10,
            RecipeComponentVersionId = versionId,
            ComponentName = name,
            VersionNumber = 1,
            VersionStatus = "Published",
            QuantityPerServing = quantityPerServing,
            Unit = unit,
            YieldQuantity = yieldQuantity,
            RawWeightGrams = rawWeight,
            CookedWeightGrams = cookedWeight,
            NutritionPer100g = new MealVariantNutritionDto
            {
                Calories = 100m,
                Protein = 10m,
                Carbohydrates = 20m,
                Fat = 3m,
                Fiber = 2m,
            },
            AllergensApproved = true,
            Ingredients =
            [
                new MealVariantResultIngredientInputDto
                {
                    IngredientId = versionId,
                    IngredientName = $"{name} skladnik",
                    WarehouseCategoryId = versionId,
                    WarehouseCategoryName = "Produkcja",
                    WeightInGrams = ingredientWeight,
                    YieldFactor = 0.8m,
                },
            ],
            PackagingRequirements =
            [
                CreatePackaging("RecipeComponentVersion", $"{name} opakowanie", packagingQuantity, versionId),
            ],
            Allergens =
            [
                new MealVariantResultAllergenInputDto
                {
                    AllergenId = versionId,
                    Name = $"{name} alergen",
                    SourceType = "RecipeComponentVersion",
                    SourceName = name,
                },
            ],
        };
    }

    private static MealVariantResultPackagingInputDto CreatePackaging(
        string ownerType,
        string name,
        decimal quantity,
        int? componentVersionId = null)
    {
        return new MealVariantResultPackagingInputDto
        {
            OwnerType = ownerType,
            RecipeComponentVersionId = componentVersionId,
            WarehouseCategoryId = 99,
            ResourceName = name,
            Quantity = quantity,
            Unit = "pcs",
            IsCustomerFacing = true,
        };
    }
}
