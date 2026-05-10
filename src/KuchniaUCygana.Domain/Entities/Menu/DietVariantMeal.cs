using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class DietVariantMeal
{
    [AutoIncrement]
    [PrimaryKey]
    public int Id { get; set; }

    public int DietVariantId { get; set; }

    public int MealId { get; set; }

    public decimal ServingSizeMultiplier { get; set; } = 1.0m;

    public int SortOrder { get; set; }
}
