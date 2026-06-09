namespace KuchniaUCygana.Application.DTOs.Menu;

public class MealDto
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string Status { get; set; } = string.Empty;

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; }
}
