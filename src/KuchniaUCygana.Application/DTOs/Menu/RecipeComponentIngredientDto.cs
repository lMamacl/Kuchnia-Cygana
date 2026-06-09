namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class RecipeComponentIngredientDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}
