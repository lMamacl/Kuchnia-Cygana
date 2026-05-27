namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class AiDescriptionResponse
{
    public int MealId { get; set; }

    public string MarketingDescription { get; set; } = string.Empty;
}
