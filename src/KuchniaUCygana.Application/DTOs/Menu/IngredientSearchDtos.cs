namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class IngredientSearchFilterDto
{
    public string? Search { get; set; }

    public string? ResourceType { get; set; }

    public int? FoodCategoryId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public int? AllergenId { get; set; }

    public bool MissingWarehouseMapping { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class IngredientListItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ResourceType { get; set; } = "Food";

    public int? FoodCategoryId { get; set; }

    public string? FoodCategoryName { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public bool MissingWarehouseMapping { get; set; }

    public bool IsActive { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public string? AllergenNames { get; set; }

    public bool HasNutrition => CaloriesPer100g.HasValue;
}
