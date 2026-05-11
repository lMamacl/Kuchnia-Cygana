using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class MealAllergen
{
    [AutoIncrement]
    [PrimaryKey]
    public int Id { get; set; }

    public int MealId { get; set; }

    public int AllergenId { get; set; }

    public bool IsTrace { get; set; } = false;
}
