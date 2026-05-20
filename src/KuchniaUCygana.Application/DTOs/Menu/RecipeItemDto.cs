namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class RecipeItemDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public decimal WeightInGrams { get; set; }

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}
