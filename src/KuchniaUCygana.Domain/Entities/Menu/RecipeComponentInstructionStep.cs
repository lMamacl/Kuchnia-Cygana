using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("RecipeComponentInstructionSteps")]
public sealed class RecipeComponentInstructionStep : AuditableEntity
{
    public int RecipeComponentInstructionSectionId { get; set; }

    public string StepText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool RequiresControl { get; set; }

    [StringLength(80)]
    public string? ControlType { get; set; }

    public decimal? ExpectedValue { get; set; }

    [StringLength(40)]
    public string? ExpectedUnit { get; set; }

    public bool IsCritical { get; set; }
}
