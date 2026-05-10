using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class Recipe
{
    [AutoIncrement]
    [PrimaryKey]
    public int Id { get; set; }

    public int MealId { get; set; }

    public int IngredientId { get; set; }

    public decimal WeightInGrams { get; set; }

    public bool IsOptional { get; set; } = false;

    public string? Notes { get; set; }
}
