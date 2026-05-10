using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class MealImage : BaseEntity
{
    public int MealId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public bool IsMain { get; set; } = false;

    public long FileSizeBytes { get; set; }
}
