namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class DietDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? ThumbnailUrl { get; set; }

    public List<DietVariantDto> Variants { get; set; } = new();
}
