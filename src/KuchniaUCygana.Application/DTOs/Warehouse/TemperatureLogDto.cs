namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// DTO odczytu temperatury — odpowiada TemperatureLog z Domain.
/// </summary>
public sealed class TemperatureLogDto
{
    public long Id { get; set; }

    public int? HaccpLocationId { get; set; }

    public string DeviceNameOrLocation { get; set; } = string.Empty;

    public decimal RecordedTemperatureCelsius { get; set; }

    public decimal MinTemperatureCelsius { get; set; } = -25m;

    public decimal MaxTemperatureCelsius { get; set; } = 8m;

    public DateTimeOffset RecordedAt { get; set; }

    public string? Remarks { get; set; }

    /// <summary>
    /// Czy wartość jest poza zakresem HACCP (-25°C do +8°C).
    /// </summary>
    public bool IsOutOfRange { get; set; }
}
