using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.DTOs.Production;

public sealed class KitchenDashboardFilterDto
{
    public DateOnly Date { get; set; }

    public string? Search { get; set; }

    public string? Status { get; set; }

    public int? ProductionGroup { get; set; }

    public string? Fefo { get; set; }

    public string? Packaging { get; set; }

    public string? Snapshot { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class KitchenDashboardDto
{
    public KitchenDashboardFilterDto Filter { get; set; } = new();

    public ProductionPlanDto? Plan { get; set; }

    public PagedResultDto<KitchenDashboardItemDto> Items { get; set; } = new();

    public KitchenDashboardSummaryDto Summary { get; set; } = new();
}

public sealed class KitchenDashboardSummaryDto
{
    public int TotalItems { get; set; }

    public int TotalPlannedQuantity { get; set; }

    public int TotalCookedQuantity { get; set; }

    public int PlannedItems { get; set; }

    public int CookingItems { get; set; }

    public int CookedItems { get; set; }

    public int FailedItems { get; set; }

    public int PendingFefoItems { get; set; }

    public int FefoDeductedItems { get; set; }

    public int PendingPackagingItems { get; set; }

    public int PackagingDeductedItems { get; set; }

    public int SnapshotItems { get; set; }

    public int PercentComplete => TotalPlannedQuantity <= 0
        ? 0
        : Math.Min(100, TotalCookedQuantity * 100 / TotalPlannedQuantity);
}

public sealed class KitchenDashboardItemDto
{
    public int Id { get; set; }

    public int ProductionPlanId { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public int? MealVariantId { get; set; }

    public string? MealVariantName { get; set; }

    public string? CategoryName { get; set; }

    public string? MealSlot { get; set; }

    public int PlannedQuantity { get; set; }

    public int CookedQuantity { get; set; }

    public string Status { get; set; } = string.Empty;

    public int? ProductionGroup { get; set; }

    public string? EstimatedReadyTime { get; set; }

    public string? ActualReadyTime { get; set; }

    public DateTimeOffset? FefoDeductedAt { get; set; }

    public DateTimeOffset? PackagingDeductedAt { get; set; }

    public string? M2SnapshotHash { get; set; }

    public bool HasM2Snapshot { get; set; }

    public int ComponentCount { get; set; }

    public int PackagingRequirementCount { get; set; }

    public int ValidationWarningCount { get; set; }

    public bool HasMissingWarehouseMappings { get; set; }

    public string? SnapshotWarning { get; set; }

    public int ProgressPercent => PlannedQuantity <= 0
        ? 0
        : Math.Min(100, CookedQuantity * 100 / PlannedQuantity);
}
