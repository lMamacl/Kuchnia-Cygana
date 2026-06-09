namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealImageDto
{
    public int Id { get; set; }

    public int MealId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public bool IsMain { get; set; }

    public long FileSizeBytes { get; set; }
}
