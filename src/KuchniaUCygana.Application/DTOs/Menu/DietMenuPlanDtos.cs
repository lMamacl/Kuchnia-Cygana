namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class DietMenuWeekDto
{
    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int DaysCount { get; set; } = 7;

    public List<DietMenuDaySummaryDto> Days { get; set; } = new();
}

public sealed class DietMenuDaySummaryDto
{
    public int Id { get; set; }

    public DateOnly PlanDate { get; set; }

    public string Status { get; set; } = "Missing";

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public bool HasPlan => Id > 0;

    public bool CanEdit { get; set; }

    public string? EditBlockReason { get; set; }

    public int ActiveItemCount { get; set; }

    public int QuickWarningCount { get; set; }

    public bool CanPublishQuick => HasPlan && ActiveItemCount > 0 && QuickWarningCount == 0;
}

public sealed class DietMenuDayShellDto
{
    public int Id { get; set; }

    public DateOnly PlanDate { get; set; }

    public string Status { get; set; } = "Missing";

    public string? Notes { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public bool HasPlan => Id > 0;

    public bool CanEdit { get; set; }

    public string? EditBlockReason { get; set; }

    public int ActiveItemCount { get; set; }

    public int QuickWarningCount { get; set; }

    public bool CanPublishQuick => HasPlan && ActiveItemCount > 0 && QuickWarningCount == 0;

    public List<DietMenuPlanDietVariantSummaryDto> DietVariants { get; set; } = new();
}

public sealed class DietMenuPlanDietVariantSummaryDto
{
    public int DietVariantId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public bool IsDefault { get; set; }

    public int ActiveItemCount { get; set; }

    public int QuickWarningCount { get; set; }
}

public sealed class DietMenuDietVariantItemsDto
{
    public int DietMenuPlanId { get; set; }

    public DateOnly PlanDate { get; set; }

    public string PlanStatus { get; set; } = "Missing";

    public bool CanEdit { get; set; }

    public int DietVariantId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public List<DietMenuPlanItemDto> Items { get; set; } = new();

    public DietMenuPlanValidationDto Validation { get; set; } = new();
}

public sealed class DietMenuDayDto
{
    public int Id { get; set; }

    public DateOnly PlanDate { get; set; }

    public string Status { get; set; } = "Missing";

    public string? Notes { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public bool HasPlan => Id > 0;

    public bool CanEdit { get; set; }

    public string? EditBlockReason { get; set; }

    public List<DietMenuPlanItemDto> Items { get; set; } = new();

    public DietMenuPlanValidationDto Validation { get; set; } = new();
}

public sealed class DietMenuPlanItemDto
{
    public int Id { get; set; }

    public int DietMenuPlanId { get; set; }

    public int DietVariantId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string? MealVariantStatus { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string MealStatus { get; set; } = string.Empty;

    public string MealSlot { get; set; } = string.Empty;

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public bool UsesSpecialMealVariant => MealVariantId.HasValue;

    public bool UsesServingMultiplier => ServingSizeMultiplier != 1.0m;

    public decimal? FinalWeightGrams { get; set; }

    public decimal? FinalWeightAfterMultiplierGrams { get; set; }

    public string CompletenessStatus { get; set; } = "Incomplete";

    public int SortOrder { get; set; }

    public int ComponentCount { get; set; }

    public int LegacyRecipeCount { get; set; }

    public bool HasRecipeSource => ComponentCount > 0 || LegacyRecipeCount > 0;

    public bool IsMealPublished { get; set; }

    public bool IsRecipeValid { get; set; }

    public bool IsResultComplete { get; set; }

    public List<string> ValidationWarnings { get; set; } = new();
}

public sealed class MealVariantPlanOptionDto
{
    public int MealId { get; set; }

    public int MealVariantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public bool IsDefault { get; set; }
}

public readonly record struct MealVariantResultKey(int MealId, int? MealVariantId);

public enum MealVariantResultCacheMode
{
    CachePreferred = 0,
    Fresh = 1,
}

public sealed class MenuPlanMealLookupDto
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string Status { get; set; } = string.Empty;

    public int VariantCount { get; set; }

    public int WarningCount { get; set; }

    public string CompletenessStatus { get; set; } = "Incomplete";

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class MenuPlanMealVariantLookupDto
{
    public int? MealVariantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public bool IsDefault { get; set; }

    public decimal? FinalWeightGrams { get; set; }

    public int WarningCount { get; set; }

    public string CompletenessStatus { get; set; } = "Incomplete";

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class DietMenuPlanValidationDto
{
    public bool CanPublish => Warnings.Count == 0;

    public List<string> Warnings { get; set; } = new();
}

public sealed class MealPlanSearchItemDto
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ComponentCount { get; set; }

    public int LegacyRecipeCount { get; set; }
}

public sealed class CreateDietMenuPlanRequest
{
    public DateOnly PlanDate { get; set; }

    public string? Notes { get; set; }
}

public sealed class AddDietMenuPlanItemRequest
{
    public int DietMenuPlanId { get; set; }

    public int DietVariantId { get; set; }

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public string MealSlot { get; set; } = "Breakfast";

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public int SortOrder { get; set; }
}

public sealed class UpdateDietMenuPlanItemRequest
{
    public int Id { get; set; }

    public int DietMenuPlanId { get; set; }

    public int DietVariantId { get; set; }

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public string MealSlot { get; set; } = "Breakfast";

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public int SortOrder { get; set; }
}

public sealed class CopyDietMenuDayRequest
{
    public int SourcePlanId { get; set; }

    public DateOnly TargetDate { get; set; }

    public bool ClearTargetDraft { get; set; } = true;
}

public sealed class PublishDietMenuPlanRequest
{
    public int DietMenuPlanId { get; set; }
}
