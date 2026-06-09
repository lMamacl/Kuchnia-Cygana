namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class UpdateDietRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public bool IsActive { get; set; }
}
