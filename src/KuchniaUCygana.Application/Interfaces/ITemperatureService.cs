using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny HACCP — logowanie temperatur i raporty.
/// </summary>
public interface ITemperatureService
{
    /// <summary>
    /// Loguje odczyt temperatury.
    /// </summary>
    Task<TemperatureLogDto> LogTemperatureAsync(LogTemperatureRequest request);

    /// <summary>
    /// Generuje raport HACCP za podany zakres dat.
    /// </summary>
    Task<HaccpReportDto> GetHaccpReportAsync(DateOnly from, DateOnly to);

    Task<IReadOnlyList<HaccpLocationDto>> GetActiveLocationsAsync();

    /// <summary>
    /// Pobiera dane do wykresu temperatur z danej lokalizacji.
    /// </summary>
    Task<IEnumerable<TemperatureChartDataDto>> GetChartDataAsync(string device, int days);

    /// <summary>
    /// Eksportuje logi temperatur do formatu CSV za podany okres.
    /// </summary>
    Task<string> ExportHaccpCsvAsync(DateTimeOffset from, DateTimeOffset to);
}
