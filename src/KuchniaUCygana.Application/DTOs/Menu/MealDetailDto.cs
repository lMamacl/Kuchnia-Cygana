namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealDetailDto : MealDto
{
    public List<RecipeItemDto> Recipe { get; set; } = new();

    public List<MealImageDto> Images { get; set; } = new();
}
