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

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? FefoDeductedAt { get; set; }

    public DateTimeOffset? PackagingDeductedAt { get; set; }

    public bool HasM2Snapshot { get; set; }

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

    public List<ProductionAdjustmentApprovalDto> AdjustmentApprovals { get; set; } = new();

    public List<string> ApprovalBlockers { get; set; } = new();

    public bool CanApproveCooking => ApprovalBlockers.Count == 0;
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

    public List<CookingComponentInstructionSectionDto> InstructionSections { get; set; } = new();

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public List<CookingCardIngredientDto> Ingredients { get; set; } = new();

    public List<CookingCardPackagingDto> PackagingRequirements { get; set; } = new();

    public string SessionStatus { get; set; } = "NotStarted";

    public int TotalStepCount { get; set; }

    public int CheckedStepCount { get; set; }

    public int RequiredStepCount { get; set; }

    public int RequiredCheckedStepCount { get; set; }

    public bool IsSessionCompleted => string.Equals(SessionStatus, "Completed", StringComparison.OrdinalIgnoreCase);

    public int ProgressPercent => TotalStepCount <= 0
        ? 0
        : Math.Min(100, CheckedStepCount * 100 / TotalStepCount);

    public bool RequiresAttention => RequiredStepCount > RequiredCheckedStepCount
        && (CheckedStepCount > 0 || string.Equals(SessionStatus, "InProgress", StringComparison.OrdinalIgnoreCase));

    public string StatusLabel
    {
        get
        {
            if (IsSessionCompleted)
            {
                return "ukończona";
            }

            if (RequiresAttention)
            {
                return "wymaga kontroli";
            }

            if (CheckedStepCount > 0 || string.Equals(SessionStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                return "w toku";
            }

            return "nie rozpoczęta";
        }
    }

    public string StatusColor => StatusLabel switch
    {
        "ukończona" => "success",
        "wymaga kontroli" => "warning",
        "w toku" => "blue",
        _ => "secondary",
    };

    public string? ControlMessage => RequiredStepCount <= 0
        ? null
        : RequiredCheckedStepCount >= RequiredStepCount
            ? "Kontrole temperatury i kroki krytyczne są odznaczone."
            : $"Do odznaczenia: {RequiredStepCount - RequiredCheckedStepCount} kontrola/krok krytyczny.";
}

public sealed class CookingComponentCardDto
{
    public int PlanItemId { get; set; }

    public int ProductionPlanId { get; set; }

    public DateOnly ProductionDate { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public int DietVariantId { get; set; }

    public int PlannedQuantity { get; set; }

    public string ProductionStatus { get; set; } = string.Empty;

    public DateTimeOffset? FefoDeductedAt { get; set; }

    public DateTimeOffset? PackagingDeductedAt { get; set; }

    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public decimal TotalQuantity { get; set; }

    public string Unit { get; set; } = "portion";

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = Array.Empty<string>();

    public CookingCardNutritionDto? NutritionFacts { get; set; }

    public List<CookingComponentIngredientDto> Ingredients { get; set; } = new();

    public List<CookingCardPackagingDto> PackagingRequirements { get; set; } = new();

    public List<CookingComponentInstructionSectionDto> InstructionSections { get; set; } = new();

    public CookingComponentSessionDto Session { get; set; } = new();
}

public sealed class CookingComponentIngredientDto
{
    public int IngredientId { get; set; }

    public int? StockItemId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public string? WarehouseCategoryName { get; set; }

    public decimal WeightPerServing { get; set; }

    public decimal TotalWeight { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsOptional { get; set; }
}

public sealed class CookingComponentInstructionSectionDto
{
    public int SectionId { get; set; }

    public string? Title { get; set; }

    public int SortOrder { get; set; }

    public List<CookingComponentInstructionStepDto> Steps { get; set; } = new();
}

public sealed class CookingComponentInstructionStepDto
{
    public int StepId { get; set; }

    public string StepText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool RequiresControl { get; set; }

    public string? ControlType { get; set; }

    public decimal? ExpectedValue { get; set; }

    public string? ExpectedUnit { get; set; }

    public bool IsCritical { get; set; }
}

public sealed class CookingComponentSessionDto
{
    public int? SessionId { get; set; }

    public string Status { get; set; } = "NotStarted";

    public DateTimeOffset? StartedAt { get; set; }

    public string? StartedBy { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? CompletedBy { get; set; }

    public Dictionary<int, CookingStepCheckDto> StepChecksByStepId { get; set; } = new();
}

public sealed class CookingStepCheckDto
{
    public int StepId { get; set; }

    public string Status { get; set; } = "Pending";

    public bool IsChecked => Status == "Checked";

    public DateTimeOffset? CheckedAt { get; set; }

    public string? CheckedBy { get; set; }

    public decimal? ActualValue { get; set; }

    public string? ActualUnit { get; set; }

    public string? Notes { get; set; }
}

public sealed class ToggleCookingStepRequest
{
    public int ProductionPlanItemId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public int StepId { get; set; }

    public bool IsChecked { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public decimal? ActualValue { get; set; }

    public string? ActualUnit { get; set; }

    public string? Notes { get; set; }
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
