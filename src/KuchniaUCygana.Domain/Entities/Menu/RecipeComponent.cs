using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("RecipeComponents")]
public sealed class RecipeComponent : AuditableEntity
{
    public int? CategoryId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}
