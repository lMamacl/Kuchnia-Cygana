using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class MealVariantResultCalculator : IMealVariantResultCalculator
{
    private const string AggregatedSource = "Aggregated";

    public MealVariantResultDto Calculate(MealVariantResultCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var usesVariant = request.MealVariantId.HasValue;
        var selectedComponents = usesVariant ? request.VariantComponents : request.BaseComponents;

        if (selectedComponents.Count == 0)
        {
            warnings.Add(usesVariant
                ? "wariant dania nie ma skladowych"
                : "posilek nie ma skladowych");
        }

        var componentResults = selectedComponents
            .OrderBy(component => component.SortOrder)
            .ThenBy(component => component.RecipeComponentVersionId)
            .Select(BuildComponentResult)
            .ToList();

        foreach (var component in componentResults)
        {
            warnings.AddRange(component.ValidationWarnings);
        }

        var aggregatedRawWeight = SumNullable(componentResults.Select(component => component.RawWeightGrams));
        var aggregatedCookedWeight = SumNullable(componentResults.Select(component => component.CookedWeightGrams));
        var aggregatedFinalWeight = aggregatedCookedWeight ?? aggregatedRawWeight;
        var nutritionSource = NormalizeNutritionSource(request.NutritionSource, warnings);
        var isAggregated = string.Equals(nutritionSource, AggregatedSource, StringComparison.OrdinalIgnoreCase);

        if (!isAggregated && string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            warnings.Add("reczny override wyniku wymaga powodu");
        }

        var finalRawWeight = isAggregated
            ? aggregatedRawWeight
            : request.ManualRawWeightGrams ?? aggregatedRawWeight;
        var finalCookedWeight = isAggregated
            ? aggregatedCookedWeight
            : request.ManualCookedWeightGrams ?? aggregatedCookedWeight;
        var finalWeight = finalCookedWeight ?? finalRawWeight;

        if (!finalWeight.HasValue || finalWeight.Value <= 0)
        {
            warnings.Add("brak finalnej gramatury porcji");
        }

        var nutritionPer100g = isAggregated
            ? CalculateAggregatedNutrition(componentResults, finalWeight)
            : request.ManualNutritionPer100g;

        if (nutritionPer100g is null)
        {
            warnings.Add(isAggregated
                ? "brak pelnego nutrition do agregacji"
                : "brak recznego nutrition dla override");
        }

        if (!request.AllergensApproved)
        {
            warnings.Add("alergeny wyniku nie sa zatwierdzone");
        }

        var mealPackaging = request.MealPackagingRequirements.Select(packaging => MapPackaging(packaging, 1.0m));
        var variantPackaging = usesVariant
            ? request.VariantPackagingRequirements.Select(packaging => MapPackaging(packaging, 1.0m))
            : Enumerable.Empty<MealVariantResultPackagingDto>();
        var packagingRequirements = AggregatePackaging(
            componentResults.SelectMany(component => component.PackagingRequirements)
                .Concat(mealPackaging)
                .Concat(variantPackaging));

        if (packagingRequirements.Count == 0)
        {
            warnings.Add("brak wymaganych opakowan");
        }

        foreach (var packaging in packagingRequirements)
        {
            if (!packaging.StockItemId.HasValue && !packaging.WarehouseCategoryId.HasValue)
            {
                warnings.Add($"opakowanie '{packaging.ResourceName}' nie ma mapowania magazynowego");
            }
        }

        var allergens = AggregateAllergens(
            componentResults.SelectMany(component => component.Allergens)
                .Concat(request.MealAllergens.Select(MapAllergen))
                .Concat(usesVariant ? request.VariantAllergens.Select(MapAllergen) : Enumerable.Empty<MealVariantResultAllergenDto>()));
        var ingredients = AggregateIngredients(componentResults.SelectMany(component => component.Ingredients));
        var distinctWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MealVariantResultDto
        {
            MealId = request.MealId,
            MealName = request.MealName,
            MealVariantId = request.MealVariantId,
            MealVariantName = request.MealVariantName,
            VariantType = request.VariantType,
            FinalRawWeightGrams = finalRawWeight,
            FinalCookedWeightGrams = finalCookedWeight,
            FinalWeightGrams = finalWeight,
            NutritionPer100g = nutritionPer100g,
            NutritionPerServing = nutritionPer100g is not null && finalWeight.HasValue && finalWeight.Value > 0
                ? ScaleNutrition(nutritionPer100g, finalWeight.Value / 100m)
                : null,
            Allergens = allergens,
            Ingredients = ingredients,
            PackagingRequirements = packagingRequirements,
            Components = componentResults,
            IsComplete = distinctWarnings.Count == 0,
            CompletenessStatus = distinctWarnings.Count == 0 ? "Complete" : "Incomplete",
            ValidationWarnings = distinctWarnings,
            IsAggregated = isAggregated,
            NutritionSource = nutritionSource,
            OverrideReason = request.OverrideReason,
        };
    }

    private static MealVariantResultComponentDto BuildComponentResult(MealVariantResultComponentInputDto component)
    {
        var warnings = new List<string>();
        var scaleFactor = CalculateScaleFactor(component, warnings);
        var rawWeight = component.RawWeightGrams.HasValue ? component.RawWeightGrams.Value * scaleFactor : (decimal?)null;
        var cookedWeight = component.CookedWeightGrams.HasValue ? component.CookedWeightGrams.Value * scaleFactor : (decimal?)null;
        var finalWeight = cookedWeight ?? rawWeight;

        if (!IsPublished(component.VersionStatus))
        {
            warnings.Add($"{component.ComponentName}: wersja skladowej nie jest opublikowana");
        }

        if (component.QuantityPerServing <= 0)
        {
            warnings.Add($"{component.ComponentName}: ilosc na porcje musi byc wieksza od zera");
        }

        if (!finalWeight.HasValue || finalWeight.Value <= 0)
        {
            warnings.Add($"{component.ComponentName}: brak finalnej gramatury skladowej");
        }

        if (component.NutritionPer100g is null)
        {
            warnings.Add($"{component.ComponentName}: brak nutrition skladowej");
        }

        if (!component.AllergensApproved)
        {
            warnings.Add($"{component.ComponentName}: alergeny skladowej nie sa zatwierdzone");
        }

        if (component.Ingredients.Count == 0)
        {
            warnings.Add($"{component.ComponentName}: brak skladnikow");
        }

        var ingredients = component.Ingredients
            .Select(ingredient => MapIngredient(ingredient, component, scaleFactor, warnings))
            .ToList();
        var packaging = component.PackagingRequirements
            .Select(requirement => MapPackaging(requirement, scaleFactor))
            .ToList();

        if (packaging.Count == 0)
        {
            warnings.Add($"{component.ComponentName}: brak opakowan skladowej");
        }

        foreach (var requirement in packaging)
        {
            if (!requirement.StockItemId.HasValue && !requirement.WarehouseCategoryId.HasValue)
            {
                warnings.Add($"{component.ComponentName}: opakowanie '{requirement.ResourceName}' nie ma mapowania magazynowego");
            }
        }

        var distinctWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MealVariantResultComponentDto
        {
            RecipeComponentId = component.RecipeComponentId,
            RecipeComponentVersionId = component.RecipeComponentVersionId,
            ComponentName = component.ComponentName,
            VersionNumber = component.VersionNumber,
            VersionStatus = component.VersionStatus,
            Role = component.Role,
            QuantityPerServing = component.QuantityPerServing,
            Unit = component.Unit,
            SortOrder = component.SortOrder,
            IsOptional = component.IsOptional,
            YieldQuantity = component.YieldQuantity,
            YieldUnit = component.YieldUnit,
            ScaleFactor = scaleFactor,
            RawWeightGrams = rawWeight,
            CookedWeightGrams = cookedWeight,
            FinalWeightGrams = finalWeight,
            NutritionPer100g = component.NutritionPer100g,
            NutritionPerServing = component.NutritionPer100g is not null && finalWeight.HasValue && finalWeight.Value > 0
                ? ScaleNutrition(component.NutritionPer100g, finalWeight.Value / 100m)
                : null,
            Ingredients = ingredients,
            PackagingRequirements = packaging,
            Allergens = AggregateAllergens(component.Allergens.Select(MapAllergen)),
            IsComplete = distinctWarnings.Count == 0,
            ValidationWarnings = distinctWarnings,
        };
    }

    private static decimal CalculateScaleFactor(MealVariantResultComponentInputDto component, List<string> warnings)
    {
        var normalizedUnit = (component.Unit ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedUnit is "portion" or "portions")
        {
            if (component.YieldQuantity <= 0)
            {
                warnings.Add($"{component.ComponentName}: yield musi byc wiekszy od zera dla jednostki portion");
                return 0m;
            }

            return component.QuantityPerServing / component.YieldQuantity;
        }

        if (normalizedUnit is "g" or "gram" or "grams")
        {
            var denominator = component.CookedWeightGrams.GetValueOrDefault() > 0
                ? component.CookedWeightGrams!.Value
                : component.RawWeightGrams.GetValueOrDefault() > 0
                    ? component.RawWeightGrams!.Value
                    : 0m;

            if (denominator <= 0)
            {
                warnings.Add($"{component.ComponentName}: jednostka g wymaga cooked/raw weight skladowej");
                return 0m;
            }

            return component.QuantityPerServing / denominator;
        }

        warnings.Add($"{component.ComponentName}: nieobslugiwana jednostka ilosci '{component.Unit}'");
        return 0m;
    }

    private static MealVariantResultIngredientDto MapIngredient(
        MealVariantResultIngredientInputDto ingredient,
        MealVariantResultComponentInputDto component,
        decimal scaleFactor,
        List<string> warnings)
    {
        var yieldFactor = ingredient.YieldFactor <= 0 ? 1.0m : ingredient.YieldFactor;
        var netWeight = ingredient.WeightInGrams * scaleFactor;
        var grossWeight = yieldFactor > 0 ? netWeight / yieldFactor : netWeight;

        if (ingredient.WeightInGrams <= 0)
        {
            warnings.Add($"{component.ComponentName}: skladnik '{ingredient.IngredientName}' ma niepoprawna gramature");
        }

        if (!ingredient.WarehouseCategoryId.HasValue)
        {
            warnings.Add($"{component.ComponentName}: skladnik '{ingredient.IngredientName}' nie ma WarehouseCategoryId");
        }

        return new MealVariantResultIngredientDto
        {
            IngredientId = ingredient.IngredientId,
            IngredientName = ingredient.IngredientName,
            StockItemId = ingredient.StockItemId,
            WarehouseCategoryId = ingredient.WarehouseCategoryId,
            WarehouseCategoryName = ingredient.WarehouseCategoryName,
            NetWeightInGrams = netWeight,
            GrossWeightInGrams = grossWeight,
            YieldFactor = yieldFactor,
            IsOptional = ingredient.IsOptional,
            Notes = ingredient.Notes,
            SourceRecipeComponentVersionIds = [component.RecipeComponentVersionId],
            SourceComponentNames = [component.ComponentName],
        };
    }

    private static MealVariantResultPackagingDto MapPackaging(
        MealVariantResultPackagingInputDto packaging,
        decimal scaleFactor)
    {
        return new MealVariantResultPackagingDto
        {
            OwnerType = packaging.OwnerType,
            MealId = packaging.MealId,
            MealVariantId = packaging.MealVariantId,
            RecipeComponentVersionId = packaging.RecipeComponentVersionId,
            StockItemId = packaging.StockItemId,
            WarehouseCategoryId = packaging.WarehouseCategoryId,
            ResourceName = packaging.ResourceName,
            Quantity = packaging.Quantity * scaleFactor,
            Unit = packaging.Unit,
            ContainerRole = packaging.ContainerRole,
            IsCustomerFacing = packaging.IsCustomerFacing,
        };
    }

    private static MealVariantResultAllergenDto MapAllergen(MealVariantResultAllergenInputDto allergen)
    {
        var source = string.IsNullOrWhiteSpace(allergen.SourceName)
            ? allergen.SourceType
            : $"{allergen.SourceType}: {allergen.SourceName}";

        return new MealVariantResultAllergenDto
        {
            AllergenId = allergen.AllergenId,
            Name = allergen.Name,
            IsTrace = allergen.IsTrace,
            Sources = string.IsNullOrWhiteSpace(source) ? Array.Empty<string>() : [source],
        };
    }

    private static IReadOnlyList<MealVariantResultIngredientDto> AggregateIngredients(
        IEnumerable<MealVariantResultIngredientDto> ingredients)
    {
        return ingredients
            .GroupBy(ingredient => new
            {
                ingredient.IngredientId,
                ingredient.IngredientName,
                ingredient.StockItemId,
                ingredient.WarehouseCategoryId,
                ingredient.WarehouseCategoryName,
                ingredient.IsOptional,
            })
            .Select(group => new MealVariantResultIngredientDto
            {
                IngredientId = group.Key.IngredientId,
                IngredientName = group.Key.IngredientName,
                StockItemId = group.Key.StockItemId,
                WarehouseCategoryId = group.Key.WarehouseCategoryId,
                WarehouseCategoryName = group.Key.WarehouseCategoryName,
                NetWeightInGrams = group.Sum(ingredient => ingredient.NetWeightInGrams),
                GrossWeightInGrams = group.Sum(ingredient => ingredient.GrossWeightInGrams),
                YieldFactor = group.Max(ingredient => ingredient.YieldFactor),
                IsOptional = group.Key.IsOptional,
                Notes = string.Join("; ", group.Select(ingredient => ingredient.Notes).Where(note => !string.IsNullOrWhiteSpace(note)).Distinct()),
                SourceRecipeComponentVersionIds = group
                    .SelectMany(ingredient => ingredient.SourceRecipeComponentVersionIds)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList(),
                SourceComponentNames = group
                    .SelectMany(ingredient => ingredient.SourceComponentNames)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList(),
            })
            .OrderBy(ingredient => ingredient.IngredientName)
            .ThenBy(ingredient => ingredient.IngredientId)
            .ToList();
    }

    private static IReadOnlyList<MealVariantResultPackagingDto> AggregatePackaging(
        IEnumerable<MealVariantResultPackagingDto> requirements)
    {
        return requirements
            .GroupBy(requirement => new
            {
                requirement.OwnerType,
                requirement.MealId,
                requirement.MealVariantId,
                requirement.RecipeComponentVersionId,
                requirement.StockItemId,
                requirement.WarehouseCategoryId,
                requirement.ResourceName,
                requirement.Unit,
                requirement.ContainerRole,
                requirement.IsCustomerFacing,
            })
            .Select(group => new MealVariantResultPackagingDto
            {
                OwnerType = group.Key.OwnerType,
                MealId = group.Key.MealId,
                MealVariantId = group.Key.MealVariantId,
                RecipeComponentVersionId = group.Key.RecipeComponentVersionId,
                StockItemId = group.Key.StockItemId,
                WarehouseCategoryId = group.Key.WarehouseCategoryId,
                ResourceName = group.Key.ResourceName,
                Quantity = group.Sum(requirement => requirement.Quantity),
                Unit = group.Key.Unit,
                ContainerRole = group.Key.ContainerRole,
                IsCustomerFacing = group.Key.IsCustomerFacing,
            })
            .OrderBy(requirement => requirement.OwnerType)
            .ThenBy(requirement => requirement.ResourceName)
            .ToList();
    }

    private static IReadOnlyList<MealVariantResultAllergenDto> AggregateAllergens(
        IEnumerable<MealVariantResultAllergenDto> allergens)
    {
        return allergens
            .Where(allergen => !string.IsNullOrWhiteSpace(allergen.Name))
            .GroupBy(allergen => allergen.AllergenId.HasValue
                ? $"id:{allergen.AllergenId.Value}"
                : $"name:{allergen.Name.Trim().ToLowerInvariant()}")
            .Select(group =>
            {
                var first = group.First();
                return new MealVariantResultAllergenDto
                {
                    AllergenId = first.AllergenId,
                    Name = first.Name,
                    IsTrace = group.All(allergen => allergen.IsTrace),
                    Sources = group
                        .SelectMany(allergen => allergen.Sources)
                        .Where(source => !string.IsNullOrWhiteSpace(source))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(source => source)
                        .ToList(),
                };
            })
            .OrderBy(allergen => allergen.Name)
            .ToList();
    }

    private static MealVariantNutritionDto? CalculateAggregatedNutrition(
        IReadOnlyList<MealVariantResultComponentDto> components,
        decimal? finalWeight)
    {
        if (!finalWeight.HasValue || finalWeight.Value <= 0)
        {
            return null;
        }

        var nutritionComponents = components
            .Where(component => component.NutritionPer100g is not null
                && component.FinalWeightGrams.HasValue
                && component.FinalWeightGrams.Value > 0)
            .ToList();

        if (nutritionComponents.Count != components.Count || nutritionComponents.Count == 0)
        {
            return null;
        }

        var total = new MealVariantNutritionDto();
        foreach (var component in nutritionComponents)
        {
            AddNutrition(total, ScaleNutrition(component.NutritionPer100g!, component.FinalWeightGrams!.Value / 100m));
        }

        return ScaleNutrition(total, 100m / finalWeight.Value);
    }

    private static MealVariantNutritionDto ScaleNutrition(MealVariantNutritionDto nutrition, decimal factor)
        => new()
        {
            Calories = nutrition.Calories * factor,
            Protein = nutrition.Protein * factor,
            Carbohydrates = nutrition.Carbohydrates * factor,
            Fat = nutrition.Fat * factor,
            Fiber = nutrition.Fiber * factor,
        };

    private static void AddNutrition(MealVariantNutritionDto target, MealVariantNutritionDto addition)
    {
        target.Calories += addition.Calories;
        target.Protein += addition.Protein;
        target.Carbohydrates += addition.Carbohydrates;
        target.Fat += addition.Fat;
        target.Fiber += addition.Fiber;
    }

    private static decimal? SumNullable(IEnumerable<decimal?> values)
    {
        var materialized = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return materialized.Count == 0 ? null : materialized.Sum();
    }

    private static string NormalizeNutritionSource(string? source, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return AggregatedSource;
        }

        var normalized = source.Trim();
        if (string.Equals(normalized, AggregatedSource, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Manual", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Override", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        warnings.Add($"nieznane zrodlo nutrition '{source}'");
        return normalized;
    }

    private static bool IsPublished(string status)
        => string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
}
