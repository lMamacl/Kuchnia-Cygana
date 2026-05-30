namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class HaccpLocationDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal MinTemperatureCelsius { get; set; }

    public decimal MaxTemperatureCelsius { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public string? Notes { get; set; }

    public IReadOnlyList<int> CategoryIds { get; set; } = Array.Empty<int>();

    public string CategoryNames { get; set; } = string.Empty;

    public string Label => $"{Name} (Limit: {MinTemperatureCelsius:F1}°C do {MaxTemperatureCelsius:F1}°C)";
}
