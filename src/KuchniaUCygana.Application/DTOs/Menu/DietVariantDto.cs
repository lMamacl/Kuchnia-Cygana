namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class DietVariantDto
{
    public int Id { get; set; }

    public int DietId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public decimal PriceMultiplier { get; set; }

    public bool IsDefault { get; set; }
}
