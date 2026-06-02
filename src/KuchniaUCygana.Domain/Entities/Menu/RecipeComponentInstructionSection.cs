using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("RecipeComponentInstructionSections")]
public sealed class RecipeComponentInstructionSection : AuditableEntity
{
    public int RecipeComponentVersionId { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    public int SortOrder { get; set; }
}
