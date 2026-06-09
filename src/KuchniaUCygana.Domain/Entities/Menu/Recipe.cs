using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("Recipes")]
public sealed class Recipe
{
    [Key]
    public int Id { get; set; }

    public int MealId { get; set; }

    public int IngredientId { get; set; }

    public decimal WeightInGrams { get; set; }

    public bool IsOptional { get; set; } = false;

    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
}
