namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class IngredientDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }
}
