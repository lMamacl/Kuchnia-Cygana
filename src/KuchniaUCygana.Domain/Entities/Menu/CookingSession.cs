using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("CookingSessions")]
public sealed class CookingSession : AuditableEntity
{
    public int RecipeComponentVersionId { get; set; }

    public DateOnly ProductionDate { get; set; }

    public int? ProductionPlanItemId { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Draft";

    public DateTimeOffset? StartedAt { get; set; }

    [StringLength(100)]
    public string? StartedBy { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [StringLength(100)]
    public string? CompletedBy { get; set; }
}
