namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class NutritionFactDto
{
    public int Id { get; set; }

    public decimal CaloriesPer100g { get; set; }

    public decimal ProteinPer100g { get; set; }

    public decimal CarbohydratesPer100g { get; set; }

    public decimal FatPer100g { get; set; }

    public decimal FiberPer100g { get; set; }
}
