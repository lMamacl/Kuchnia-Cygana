namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// Raport HACCP — podsumowanie odczytów temperatur w podanym zakresie dat.
/// </summary>
public sealed class HaccpReportDto
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public int TotalReadings { get; set; }

    public int OutOfRangeReadings { get; set; }

    public List<TemperatureLogDto> Readings { get; set; } = new();

    public IReadOnlyList<HaccpLocationDto> Locations { get; set; } = Array.Empty<HaccpLocationDto>();
}
