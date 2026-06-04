namespace KuchniaUCygana.Domain.Interfaces.External;

public sealed class PublishedDietPlanSnapshotDto
{
    public int DietMenuPlanId { get; set; }

    public DateOnly PlanDate { get; set; }

    public string PlanStatus { get; set; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public IReadOnlyList<PublishedDietPlanItemDto> Items { get; set; } = Array.Empty<PublishedDietPlanItemDto>();

    public IReadOnlyList<PlanChangeAlertDto> Alerts { get; set; } = Array.Empty<PlanChangeAlertDto>();
}

public sealed class PublishedDietPlanItemDto
{
    public int DietMenuPlanItemId { get; set; }

    public int DietMenuPlanId { get; set; }

    public DateOnly PlanDate { get; set; }

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int DietVariantId { get; set; }

    public string MealSlot { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public decimal ServingMultiplier { get; set; } = 1.0m;

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    public LabelNutritionDto? Nutrition { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = Array.Empty<string>();

    public IReadOnlyList<MealComponentVersionDto> Components { get; set; } = Array.Empty<MealComponentVersionDto>();

    public IReadOnlyList<PackagingRequirementDto> PackagingRequirements { get; set; } = Array.Empty<PackagingRequirementDto>();

    public IReadOnlyList<ComponentInstructionSectionDto> InstructionSections { get; set; } = Array.Empty<ComponentInstructionSectionDto>();

    public IReadOnlyList<string> ValidationWarnings { get; set; } = Array.Empty<string>();

    public bool IsCompleteForProduction { get; set; }
}

public sealed class MealComponentVersionDto
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

    public string? Instructions { get; set; }

    public decimal YieldQuantity { get; set; } = 1.0m;

    public string YieldUnit { get; set; } = "portion";

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    public LabelNutritionDto? Nutrition { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = Array.Empty<string>();

    public IReadOnlyList<ComponentIngredientDto> Ingredients { get; set; } = Array.Empty<ComponentIngredientDto>();

    public IReadOnlyList<PackagingRequirementDto> PackagingRequirements { get; set; } = Array.Empty<PackagingRequirementDto>();

    public IReadOnlyList<ComponentInstructionSectionDto> InstructionSections { get; set; } = Array.Empty<ComponentInstructionSectionDto>();

    public IReadOnlyList<string> ValidationWarnings { get; set; } = Array.Empty<string>();

    public bool IsCompleteForProduction { get; set; }
}

public sealed class ComponentIngredientDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public string? WarehouseCategoryCode { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}

public sealed class ComponentInstructionSectionDto
{
    public int SectionId { get; set; }

    public string? Title { get; set; }

    public int SortOrder { get; set; }

    public IReadOnlyList<ComponentInstructionStepDto> Steps { get; set; } = Array.Empty<ComponentInstructionStepDto>();
}

public sealed class ComponentInstructionStepDto
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

public sealed class PackagingRequirementDto
{
    public string OwnerType { get; set; } = string.Empty;

    public int? MealId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; }
}

public sealed class LabelNutritionDto
{
    public decimal CaloriesPer100g { get; set; }

    public decimal ProteinPer100g { get; set; }

    public decimal CarbohydratesPer100g { get; set; }

    public decimal FatPer100g { get; set; }

    public decimal FiberPer100g { get; set; }

    public decimal? CaloriesPerServing { get; set; }

    public decimal? ProteinPerServing { get; set; }

    public decimal? CarbohydratesPerServing { get; set; }

    public decimal? FatPerServing { get; set; }

    public decimal? FiberPerServing { get; set; }
}

public sealed class PlanChangeAlertDto
{
    public int Id { get; set; }

    public DateOnly PlanDate { get; set; }

    public int? DietMenuPlanId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public int? MealId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public string AlertType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public bool RequiresAcknowledgement { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public string? AcknowledgedBy { get; set; }
}

public sealed class DietPlanEntry
{
    public DateOnly PlanDate { get; set; }

    public string PlanStatus { get; set; } = string.Empty;

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int DietVariantId { get; set; }

    public string MealSlot { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public decimal ServingMultiplier { get; set; } = 1.0m;

    public decimal ServingWeightGrams { get; set; }
}

public sealed class RecipeIngredientEntry
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public string? WarehouseCategoryCode { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsOptional { get; set; }
}

public sealed class NutritionFactsEntry
{
    public decimal CaloriesPer100g { get; set; }

    public decimal ProteinPer100g { get; set; }

    public decimal CarbohydratesPer100g { get; set; }

    public decimal FatPer100g { get; set; }

    public decimal FiberPer100g { get; set; }
}

public sealed class MealCookingDetailsEntry
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? Description { get; set; }

    public string? PreparationInstructions { get; set; }

    public string? MainImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public NutritionFactsEntry? NutritionFacts { get; set; }

    public IReadOnlyList<string> Allergens { get; set; } = Array.Empty<string>();
}

public interface IDietDataProvider
{
    Task<PublishedDietPlanSnapshotDto?> GetPublishedPlanSnapshotAsync(DateOnly date);

    Task<IEnumerable<DietPlanEntry>> Get7DayPlanAsync(DateOnly startDate);

    Task<IEnumerable<DietPlanEntry>> GetPlanForDateAsync(DateOnly date);

    Task<IEnumerable<RecipeIngredientEntry>> GetRecipeForMealAsync(int mealId);

    Task<MealCookingDetailsEntry?> GetMealCookingDetailsAsync(int mealId);
}
