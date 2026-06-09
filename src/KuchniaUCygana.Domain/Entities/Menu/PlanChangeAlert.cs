using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("PlanChangeAlerts")]
public sealed class PlanChangeAlert : BaseEntity
{
    public DateOnly PlanDate { get; set; }

    public int? DietMenuPlanId { get; set; }

    public int? DietMenuPlanItemId { get; set; }

    public int? MealId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    [StringLength(60)]
    public string AlertType { get; set; } = string.Empty;

    [StringLength(30)]
    public string Severity { get; set; } = "Info";

    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; set; }

    public bool RequiresAcknowledgement { get; set; } = true;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    [StringLength(100)]
    public string? AcknowledgedBy { get; set; }
}
