namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// Request logowania temperatury — odczyt z urządzenia/lokalizacji.
/// </summary>
public sealed class LogTemperatureRequest
{
    public int? HaccpLocationId { get; set; }

    /// <summary>
    /// Nazwa urządzenia lub lokalizacji (np. "Chłodnia główna", "Lodówka nr 3").
    /// </summary>
    public string DeviceNameOrLocation { get; set; } = string.Empty;

    /// <summary>
    /// Odczytana temperatura w °C.
    /// </summary>
    public decimal TemperatureCelsius { get; set; }

    /// <summary>
    /// Uwagi (opcjonalne).
    /// </summary>
    public string? Remarks { get; set; }
}
