namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealVariantResultCalculationRequest
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string? VariantType { get; set; }

    public string NutritionSource { get; set; } = "Aggregated";

    public string? OverrideReason { get; set; }

    public decimal? ManualRawWeightGrams { get; set; }

    public decimal? ManualCookedWeightGrams { get; set; }

    public MealVariantNutritionDto? ManualNutritionPer100g { get; set; }

    public bool AllergensApproved { get; set; }

    public IReadOnlyList<MealVariantResultComponentInputDto> BaseComponents { get; set; } =
        Array.Empty<MealVariantResultComponentInputDto>();

    public IReadOnlyList<MealVariantResultComponentInputDto> VariantComponents { get; set; } =
        Array.Empty<MealVariantResultComponentInputDto>();

    public IReadOnlyList<MealVariantResultPackagingInputDto> MealPackagingRequirements { get; set; } =
        Array.Empty<MealVariantResultPackagingInputDto>();

    public IReadOnlyList<MealVariantResultPackagingInputDto> VariantPackagingRequirements { get; set; } =
        Array.Empty<MealVariantResultPackagingInputDto>();

    public IReadOnlyList<MealVariantResultAllergenInputDto> MealAllergens { get; set; } =
        Array.Empty<MealVariantResultAllergenInputDto>();

    public IReadOnlyList<MealVariantResultAllergenInputDto> VariantAllergens { get; set; } =
        Array.Empty<MealVariantResultAllergenInputDto>();
}

public sealed class MealVariantResultDto
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string? VariantType { get; set; }

    public decimal? FinalRawWeightGrams { get; set; }

    public decimal? FinalCookedWeightGrams { get; set; }

    public decimal? FinalWeightGrams { get; set; }

    public MealVariantNutritionDto? NutritionPer100g { get; set; }

    public MealVariantNutritionDto? NutritionPerServing { get; set; }

    public IReadOnlyList<MealVariantResultAllergenDto> Allergens { get; set; } =
        Array.Empty<MealVariantResultAllergenDto>();

    public IReadOnlyList<MealVariantResultIngredientDto> Ingredients { get; set; } =
        Array.Empty<MealVariantResultIngredientDto>();

    public IReadOnlyList<MealVariantResultPackagingDto> PackagingRequirements { get; set; } =
        Array.Empty<MealVariantResultPackagingDto>();

    public IReadOnlyList<MealVariantResultComponentDto> Components { get; set; } =
        Array.Empty<MealVariantResultComponentDto>();

    public bool IsComplete { get; set; }

    public string CompletenessStatus { get; set; } = "Incomplete";

    public IReadOnlyList<string> ValidationWarnings { get; set; } = Array.Empty<string>();

    public bool IsAggregated { get; set; }

    public string NutritionSource { get; set; } = "Aggregated";

    public string? OverrideReason { get; set; }
}

public sealed class MealVariantResultComponentInputDto
{
    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; } = 1.0m;

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public bool IsOptional { get; set; }

    public decimal YieldQuantity { get; set; } = 1.0m;

    public string YieldUnit { get; set; } = "portion";

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public MealVariantNutritionDto? NutritionPer100g { get; set; }

    public bool AllergensApproved { get; set; }

    public IReadOnlyList<MealVariantResultIngredientInputDto> Ingredients { get; set; } =
        Array.Empty<MealVariantResultIngredientInputDto>();

    public IReadOnlyList<MealVariantResultPackagingInputDto> PackagingRequirements { get; set; } =
        Array.Empty<MealVariantResultPackagingInputDto>();

    public IReadOnlyList<MealVariantResultAllergenInputDto> Allergens { get; set; } =
        Array.Empty<MealVariantResultAllergenInputDto>();
}

public sealed class MealVariantResultComponentDto
{
    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public bool IsOptional { get; set; }

    public decimal YieldQuantity { get; set; }

    public string YieldUnit { get; set; } = "portion";

    public decimal ScaleFactor { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? FinalWeightGrams { get; set; }

    public MealVariantNutritionDto? NutritionPer100g { get; set; }

    public MealVariantNutritionDto? NutritionPerServing { get; set; }

    public IReadOnlyList<MealVariantResultIngredientDto> Ingredients { get; set; } =
        Array.Empty<MealVariantResultIngredientDto>();

    public IReadOnlyList<MealVariantResultPackagingDto> PackagingRequirements { get; set; } =
        Array.Empty<MealVariantResultPackagingDto>();

    public IReadOnlyList<MealVariantResultAllergenDto> Allergens { get; set; } =
        Array.Empty<MealVariantResultAllergenDto>();

    public bool IsComplete { get; set; }

    public IReadOnlyList<string> ValidationWarnings { get; set; } = Array.Empty<string>();
}

public sealed class MealVariantResultIngredientInputDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}

public sealed class MealVariantResultIngredientDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public decimal NetWeightInGrams { get; set; }

    public decimal GrossWeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }

    public IReadOnlyList<int> SourceRecipeComponentVersionIds { get; set; } = Array.Empty<int>();

    public IReadOnlyList<string> SourceComponentNames { get; set; } = Array.Empty<string>();
}

public sealed class MealVariantResultPackagingInputDto
{
    public string OwnerType { get; set; } = string.Empty;

    public int? MealId { get; set; }

    public int? MealVariantId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; }
}

public sealed class MealVariantResultPackagingDto
{
    public string OwnerType { get; set; } = string.Empty;

    public int? MealId { get; set; }

    public int? MealVariantId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; }
}

public sealed class MealVariantResultAllergenInputDto
{
    public int? AllergenId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsTrace { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string? SourceName { get; set; }
}

public sealed class MealVariantResultAllergenDto
{
    public int? AllergenId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsTrace { get; set; }

    public IReadOnlyList<string> Sources { get; set; } = Array.Empty<string>();
}

public sealed class MealVariantNutritionDto
{
    public decimal Calories { get; set; }

    public decimal Protein { get; set; }

    public decimal Carbohydrates { get; set; }

    public decimal Fat { get; set; }

    public decimal Fiber { get; set; }
}
