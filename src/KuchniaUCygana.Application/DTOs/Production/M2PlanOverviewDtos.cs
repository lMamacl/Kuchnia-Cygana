using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Application.DTOs.Production;

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
    public int DietVariantId { get; set; }
    public string MealSlot { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal ServingMultiplier { get; set; }
    public string CompletenessStatus { get; set; } = string.Empty;
    public bool IsCompleteForProduction { get; set; }
    public int ComponentCount { get; set; }
    public int PackagingRequirementCount { get; set; }
    public int ValidationWarningCount { get; set; }
    public List<string> ValidationWarnings { get; set; } = new();
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
