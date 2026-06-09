using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Production;

/// <summary>
/// DTO pozycji planu produkcji — odpowiada ProductionPlanItem.
/// </summary>
public sealed class ProductionPlanItemDto
{
    public int Id { get; set; }

    public int ProductionPlanId { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public string? RecipeComponentVersionIds { get; set; }

    public string? M2SnapshotHash { get; set; }

    public int PlannedQuantity { get; set; }

    public int CookedQuantity { get; set; }

    public string Status { get; set; } = string.Empty;

    // Model B-lite
    public int? ProductionGroup { get; set; }

    public string? EstimatedReadyTime { get; set; }

    public string? ActualReadyTime { get; set; }

    public DateTimeOffset? FefoDeductedAt { get; set; }

    public string? FefoReferenceDocument { get; set; }

    public DateTimeOffset? PackagingDeductedAt { get; set; }

    public string? PackagingReferenceDocument { get; set; }
}
