namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class IngredientDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ResourceType { get; set; } = "Food";

    public int? FoodCategoryId { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public string? ProductComposition { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public bool WarehouseCategoryFefoApproved { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsActive { get; set; }

    public NutritionFactDto Nutrition { get; set; } = new();

    public List<IngredientAllergenDto> Allergens { get; set; } = new();

    public List<int> SelectedAllergenIds { get; set; } = new();

    public List<int> TraceAllergenIds { get; set; } = new();
}
