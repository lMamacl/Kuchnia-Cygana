namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealDetailDto : MealDto
{
    public string? PreparationInstructions { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public MealVariantResultDto? Result { get; set; }

    public List<RecipeItemDto> Recipe { get; set; } = new();

    public List<MealRecipeComponentDto> Components { get; set; } = new();

    public List<MealVariantDto> Variants { get; set; } = new();

    public List<MealImageDto> Images { get; set; } = new();
}
