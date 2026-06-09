namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class RecipeItemDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}
