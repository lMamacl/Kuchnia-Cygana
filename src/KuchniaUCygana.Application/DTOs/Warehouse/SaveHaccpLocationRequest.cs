namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class SaveHaccpLocationRequest
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal MinTemperatureCelsius { get; set; }

    public decimal MaxTemperatureCelsius { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public string? Notes { get; set; }

    public List<int> CategoryIds { get; set; } = new();
}
