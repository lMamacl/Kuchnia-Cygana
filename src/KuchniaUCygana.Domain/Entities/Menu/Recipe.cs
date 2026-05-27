using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class Recipe
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int MealId { get; set; }

    public int IngredientId { get; set; }

    public decimal WeightInGrams { get; set; }

    public bool IsOptional { get; set; } = false;

    public string? Notes { get; set; }
}
