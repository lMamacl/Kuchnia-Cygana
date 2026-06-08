using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Production;

[Table("ProductionAdjustmentApprovals")]
public sealed class ProductionAdjustmentApproval : AuditableEntity<int>
{
    public int ProductionPlanItemId { get; set; }

    [StringLength(50)]
    public string AdjustmentType { get; set; } = "CookedQuantity";

    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    public decimal PlannedValue { get; set; }

    public decimal RequestedValue { get; set; }

    [StringLength(20)]
    public string Unit { get; set; } = "portion";

    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(120)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(120)]
    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    [StringLength(500)]
    public string? ApprovalNote { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }
}
