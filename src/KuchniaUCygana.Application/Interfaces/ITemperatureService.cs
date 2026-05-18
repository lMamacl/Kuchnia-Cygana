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
}
