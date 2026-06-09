using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IDietMenuPlanRepository
{
    Task<IReadOnlyList<DietMenuPlanDayRow>> GetPlansAsync(DateOnly startDate, DateOnly endDate);

    Task<IReadOnlyList<DietMenuPlanDaySummaryRow>> GetPlanSummariesAsync(DateOnly startDate, DateOnly endDate);

    Task<DietMenuPlanDayRow?> GetPlanByDateAsync(DateOnly date);

    Task<DietMenuPlanDayRow?> GetPlanByIdAsync(int planId);

    Task<IReadOnlyList<DietMenuPlanItemRow>> GetPlanItemsAsync(int planId);

    Task<IReadOnlyList<DietMenuPlanItemRow>> GetPlanItemsAsync(int planId, int? dietVariantId);

    Task<IReadOnlyList<DietMenuPlanDietVariantSummaryRow>> GetPlanDietVariantSummariesAsync(int planId);

    Task<DietMenuPlanItemRow?> GetPlanItemAsync(int itemId);

    Task<IReadOnlyList<MealPlanSearchRow>> GetPublishedMealsForPlanningAsync();

    Task<int> EnsurePlanAsync(DateOnly date, string? notes, string? userName);

    Task<int> AddItemAsync(DietMenuPlanItem item);

    Task UpdateItemAsync(DietMenuPlanItem item);

    Task SoftDeleteItemAsync(int itemId, string? userName);

    Task<int> CopyDayAsync(int sourcePlanId, DateOnly targetDate, string? userName, bool clearTargetDraft);

    Task PublishAsync(
        int planId,
        string? userName,
        IReadOnlyList<DietMenuPlanPublishedSnapshotRow> snapshots,
        string? planSnapshotHash);
}

public sealed class DietMenuPlanDayRow
{
    public int Id { get; set; }

    public DateOnly PlanDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public int ActiveItemCount { get; set; }
}

public sealed class DietMenuPlanDaySummaryRow
{
    public int Id { get; set; }

    public DateOnly PlanDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public int ActiveItemCount { get; set; }

    public int QuickWarningCount { get; set; }
}

public sealed class DietMenuPlanDietVariantSummaryRow
{
    public int DietVariantId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public bool IsDefault { get; set; }

    public int ActiveItemCount { get; set; }

    public int QuickWarningCount { get; set; }
}

public sealed class DietMenuPlanItemRow
{
    public int Id { get; set; }

    public int DietMenuPlanId { get; set; }

    public DateOnly PlanDate { get; set; }

    public string PlanStatus { get; set; } = string.Empty;

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

    public decimal ServingSizeMultiplier { get; set; }

    public int SortOrder { get; set; }

    public int ComponentCount { get; set; }

    public int LegacyRecipeCount { get; set; }

    public bool HasNutrition { get; set; }

    public int AllergenCount { get; set; }

    public int PackagingRequirementCount { get; set; }

    public int MissingWarehouseCategoryCount { get; set; }
}

public sealed class DietMenuPlanPublishedSnapshotRow
{
    public int DietMenuPlanItemId { get; set; }

    public string SnapshotJson { get; set; } = string.Empty;

    public string SnapshotHash { get; set; } = string.Empty;
}

public sealed class MealPlanSearchRow
{
    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ComponentCount { get; set; }

    public int LegacyRecipeCount { get; set; }
}
