namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class IngredientAllergenDto
{
    public int AllergenId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public bool TraceAmount { get; set; }
}
