namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealRecipeComponentDto
{
    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public string? Instructions { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    public List<RecipeComponentIngredientDto> Ingredients { get; set; } = new();

    public bool IsLegacyFallback => RecipeComponentVersionId <= 0;
}
