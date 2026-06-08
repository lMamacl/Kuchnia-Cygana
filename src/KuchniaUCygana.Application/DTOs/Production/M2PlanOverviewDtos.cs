using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Application.DTOs.Production;

public sealed class ProductionM2PlanFilterDto
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Days { get; set; } = 7;
}

public sealed class ProductionM2PlanOverviewDto
{
    public DateOnly StartDate { get; set; }

    public int TotalDays { get; set; }

    public int PublishedDays { get; set; }

    public int MissingPublishedDays { get; set; }

    public int TotalPlanItems { get; set; }

    public int SnapshotItemCount { get; set; }

    public int MissingSnapshotItemCount { get; set; }

    public int AlertCount { get; set; }

    public int UnacknowledgedAlertCount { get; set; }

    public List<ProductionM2PlanDayDto> Days { get; set; } = new();
}

public sealed class ProductionM2PlanDayDto
{
    public DateOnly PlanDate { get; set; }

    public int? DietMenuPlanId { get; set; }

    public string PlanStatus { get; set; } = "Missing";

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public int? ProductionPlanId { get; set; }

    public bool HasProductionPlan => ProductionPlanId.HasValue;

    public int TotalItems { get; set; }

    public int SnapshotItemCount { get; set; }

    public int MissingSnapshotItemCount { get; set; }

    public int AlertCount { get; set; }

    public int UnacknowledgedAlertCount { get; set; }

    public List<ProductionM2PlanItemDto> Items { get; set; } = new();

    public List<PlanChangeAlertDto> Alerts { get; set; } = new();
}

public sealed class ProductionM2PlanItemDto
{
    public int DietMenuPlanItemId { get; set; }

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public int DietVariantId { get; set; }

    public string? DietName { get; set; }

    public string? DietVariantName { get; set; }

    public string DietVariantLabel
        => string.Join(" / ", new[] { DietName, DietVariantName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    public string MealSlot { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public decimal ServingMultiplier { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? FinalWeightGrams { get; set; }

    public decimal? FinalWeightAfterMultiplierGrams { get; set; }

    public string CompletenessStatus { get; set; } = string.Empty;

    public bool IsCompleteForProduction { get; set; }

    public int ComponentCount { get; set; }

    public int IngredientCount { get; set; }

    public int PackagingRequirementCount { get; set; }

    public int ValidationWarningCount { get; set; }

    public List<string> ComponentNames { get; set; } = new();

    public List<string> IngredientNames { get; set; } = new();

    public List<string> PackagingNames { get; set; } = new();

    public List<string> ValidationWarnings { get; set; } = new();

    public List<string> MissingDetails { get; set; } = new();

    public List<PlanChangeAlertDto> Alerts { get; set; } = new();

    public bool HasProductionSnapshot { get; set; }

    public int? ProductionPlanItemId { get; set; }

    public decimal? PlannedQuantity { get; set; }

    public string? ProductionStatus { get; set; }

    public string? SnapshotHash { get; set; }

    public string? FreshSnapshotHash { get; set; }

    public bool IsProductionSnapshotCurrent { get; set; }

    public bool NeedsRefresh { get; set; }

    public bool CanRefresh { get; set; }

    public List<string> RefreshBlockers { get; set; } = new();
}

public sealed class M2PlanOverviewFilterDto
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int Days { get; set; } = 7;
}

public sealed class M2PlanOverviewDto
{
    public DateOnly StartDate { get; set; }

    public int TotalDays { get; set; }

    public int PublishedDays { get; set; }

    public int MissingPublishedDays { get; set; }

    public int TotalPlanItems { get; set; }

    public int SnapshotItemCount { get; set; }

    public int MissingSnapshotItemCount { get; set; }

    public int AlertCount { get; set; }

    public int UnacknowledgedAlertCount { get; set; }

    public List<M2PlanOverviewDayDto> Days { get; set; } = new();
}

public sealed class M2PlanOverviewDayDto
{
    public DateOnly PlanDate { get; set; }

    public int? DietMenuPlanId { get; set; }

    public string PlanStatus { get; set; } = "Missing";

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public int? ProductionPlanId { get; set; }

    public bool HasProductionPlan => ProductionPlanId.HasValue;

    public int TotalItems { get; set; }

    public int SnapshotItemCount { get; set; }

    public int MissingSnapshotItemCount { get; set; }

    public int AlertCount { get; set; }

    public int UnacknowledgedAlertCount { get; set; }

    public List<M2PlanOverviewItemDto> Items { get; set; } = new();

    public List<PlanChangeAlertDto> Alerts { get; set; } = new();
}

public sealed class M2PlanOverviewItemDto
{
    public int DietMenuPlanItemId { get; set; }

    public int MealId { get; set; }

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public int DietVariantId { get; set; }

    public string? DietName { get; set; }

    public string? DietVariantName { get; set; }

    public string DietVariantLabel
        => string.Join(" / ", new[] { DietName, DietVariantName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    public string MealSlot { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public decimal ServingMultiplier { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? FinalWeightGrams { get; set; }

    public decimal? FinalWeightAfterMultiplierGrams { get; set; }

    public string CompletenessStatus { get; set; } = string.Empty;

    public bool IsCompleteForProduction { get; set; }

    public int ComponentCount { get; set; }

    public int IngredientCount { get; set; }

    public int PackagingRequirementCount { get; set; }

    public int ValidationWarningCount { get; set; }

    public List<string> ComponentNames { get; set; } = new();

    public List<string> IngredientNames { get; set; } = new();

    public List<string> PackagingNames { get; set; } = new();

    public List<string> ValidationWarnings { get; set; } = new();

    public List<string> MissingDetails { get; set; } = new();

    public List<PlanChangeAlertDto> Alerts { get; set; } = new();

    public bool HasProductionSnapshot { get; set; }

    public int? ProductionPlanItemId { get; set; }

    public decimal? PlannedQuantity { get; set; }

    public string? ProductionStatus { get; set; }

    public string? SnapshotHash { get; set; }

    public string? FreshSnapshotHash { get; set; }

    public bool IsProductionSnapshotCurrent { get; set; }

    public bool NeedsRefresh { get; set; }

    public bool CanRefresh { get; set; }

    public List<string> RefreshBlockers { get; set; } = new();
}
