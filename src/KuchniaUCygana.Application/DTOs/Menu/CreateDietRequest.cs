namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class CreateDietRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<CreateDietVariantRequest> Variants { get; set; } = new();
}

public sealed class CreateDietVariantRequest
{
    public string Name { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public decimal PriceMultiplier { get; set; } = 1.0m;

    public bool IsDefault { get; set; }
}
