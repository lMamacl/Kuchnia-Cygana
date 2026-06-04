namespace KuchniaUCygana.Application.DTOs.Production;

/// <summary>
/// Karta produkcyjna (Cooking Card) — instrukcja gotowania z przeliczonymi gramaturami.
/// Złożony DTO agregujący dane z ProductionPlanItem + receptura z M2.
/// </summary>
public sealed class CookingCardDto
{
    public int PlanItemId { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string? Description { get; set; }

    public string? PreparationInstructions { get; set; }

    public string? MainImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public int DietVariantId { get; set; }

    public int PlannedQuantity { get; set; }

    public int? ProductionGroup { get; set; }

    public string? EstimatedReadyTime { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public CookingCardNutritionDto? NutritionFacts { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingWarehouseMappings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Lista składników z przeliczonymi gramaturami (na całą ilość porcji).
    /// </summary>
    public List<CookingCardIngredientDto> Ingredients { get; set; } = new();

    public List<CookingCardComponentDto> Components { get; set; } = new();

    public List<CookingCardPackagingDto> PackagingRequirements { get; set; } = new();
}

/// <summary>
/// Składnik na karcie produkcyjnej — gramatura przeliczona na liczbę porcji.
/// </summary>
public sealed class CookingCardIngredientDto
{
    public int IngredientId { get; set; }

    public int? StockItemId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public string? WarehouseCategoryName { get; set; }

    /// <summary>
    /// Gramatura na 1 porcję (z receptury M2).
    /// </summary>
    public decimal WeightPerServing { get; set; }

    /// <summary>
    /// Gramatura łączna (WeightPerServing × PlannedQuantity).
    /// </summary>
    public decimal TotalWeight { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsOptional { get; set; }
}

public sealed class CookingCardNutritionDto
{
    public decimal CaloriesPer100g { get; set; }

    public decimal ProteinPer100g { get; set; }

    public decimal CarbohydratesPer100g { get; set; }

    public decimal FatPer100g { get; set; }

    public decimal FiberPer100g { get; set; }
}

public sealed class CookingCardComponentDto
{
    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal TotalQuantity { get; set; }

    public string? Instructions { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public List<CookingCardIngredientDto> Ingredients { get; set; } = new();

    public List<CookingCardPackagingDto> PackagingRequirements { get; set; } = new();
}

public sealed class CookingCardPackagingDto
{
    public string ResourceName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public decimal QuantityPerServing { get; set; }

    public decimal TotalQuantity { get; set; }

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }
}
