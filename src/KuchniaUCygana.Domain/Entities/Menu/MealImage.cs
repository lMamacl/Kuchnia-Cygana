using KuchniaUCygana.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("MealImages")]
public sealed class MealImage : BaseEntity
{
    public int MealId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public bool IsMain { get; set; } = false;

    public long FileSizeBytes { get; set; }
}
