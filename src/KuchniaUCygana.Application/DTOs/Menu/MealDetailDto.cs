namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealDetailDto : MealDto
{
    public string? PreparationInstructions { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public List<RecipeItemDto> Recipe { get; set; } = new();

    public List<MealImageDto> Images { get; set; } = new();
}
