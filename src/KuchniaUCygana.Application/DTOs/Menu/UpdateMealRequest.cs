namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class UpdateMealRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; }
}
