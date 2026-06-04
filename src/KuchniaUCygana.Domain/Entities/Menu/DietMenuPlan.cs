using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("DietMenuPlans")]
public sealed class DietMenuPlan : AuditableEntity
{
    public DateOnly PlanDate { get; set; }

    public DietMenuPlanStatus Status { get; set; } = DietMenuPlanStatus.Draft;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    [StringLength(100)]
    public string? PublishedBy { get; set; }
}
