namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class DietMenuWeekDto
{
    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public List<DietMenuDayDto> Days { get; set; } = new();
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

    public string MealName { get; set; } = string.Empty;

    public string MealStatus { get; set; } = string.Empty;

    public string MealSlot { get; set; } = string.Empty;

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public int SortOrder { get; set; }

    public int ComponentCount { get; set; }

    public int LegacyRecipeCount { get; set; }

    public bool HasRecipeSource => ComponentCount > 0 || LegacyRecipeCount > 0;

    public bool IsMealPublished { get; set; }

    public bool IsRecipeValid { get; set; }

    public List<string> ValidationWarnings { get; set; } = new();
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
