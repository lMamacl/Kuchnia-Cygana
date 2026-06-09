using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("CookingSessionStepChecks")]
public sealed class CookingSessionStepCheck : AuditableEntity
{
    public int CookingSessionId { get; set; }

    public int RecipeComponentInstructionStepId { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Pending";

    public DateTimeOffset? CheckedAt { get; set; }

    [StringLength(100)]
    public string? CheckedBy { get; set; }

    public decimal? ActualValue { get; set; }

    [StringLength(40)]
    public string? ActualUnit { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
