namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class CreateMealRequest
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int PreparationTimeMinutes { get; set; }
}
