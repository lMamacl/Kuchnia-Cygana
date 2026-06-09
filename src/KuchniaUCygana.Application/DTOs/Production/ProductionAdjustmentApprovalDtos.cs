namespace KuchniaUCygana.Application.DTOs.Production;

public sealed class ProductionAdjustmentApprovalRequestDto
{
    public int ProductionPlanItemId { get; set; }
    public string AdjustmentType { get; set; } = "CookedQuantity";
    public decimal RequestedValue { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
}

public sealed class ProductionAdjustmentApprovalDecisionDto
{
    public int ApprovalId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public string? ApprovalNote { get; set; }
}

public sealed class ProductionAdjustmentApprovalDto
{
    public int Id { get; set; }
    public int ProductionPlanItemId { get; set; }
    public string AdjustmentType { get; set; } = "CookedQuantity";
    public string Status { get; set; } = "Pending";
    public decimal PlannedValue { get; set; }
    public decimal RequestedValue { get; set; }
    public string Unit { get; set; } = "portion";
    public string Reason { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalNote { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public decimal Difference => RequestedValue - PlannedValue;
}
