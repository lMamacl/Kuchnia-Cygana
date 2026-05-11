using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class IngredientAllergen
{
    [AutoIncrement]
    [PrimaryKey]
    public int Id { get; set; }

    public int IngredientId { get; set; }

    public int AllergenId { get; set; }

    public bool TraceAmount { get; set; } = false;
}
