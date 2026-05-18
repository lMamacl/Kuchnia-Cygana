using AutoMapper;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

/// <summary>
/// Serwis aplikacyjny HACCP — logowanie temperatur i raporty.
/// Najprostszy serwis M3 — zero zależności od innych serwisów.
/// </summary>
public sealed class TemperatureService : ITemperatureService
{
    private readonly ITemperatureLogRepository _temperatureLogRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<TemperatureService> _logger;

    public TemperatureService(
        ITemperatureLogRepository temperatureLogRepository,
        IMapper mapper,
        ILogger<TemperatureService> logger)
    {
        _temperatureLogRepository = temperatureLogRepository;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TemperatureLogDto> LogTemperatureAsync(LogTemperatureRequest request)
    {
        var entity = new TemperatureLog
        {
            DeviceNameOrLocation = request.DeviceNameOrLocation,
            RecordedTemperatureCelsius = request.TemperatureCelsius,
            RecordedAt = DateTimeOffset.UtcNow,
            Remarks = request.Remarks,
        };

        var id = await _temperatureLogRepository.InsertAsync(entity);
        entity.Id = id;

        // Alert HACCP jeśli poza zakresem
        if (request.TemperatureCelsius < -25m || request.TemperatureCelsius > 8m)
        {
            _logger.LogWarning(
                "HACCP ALERT: Temperatura {Temp}°C poza zakresem [-25, +8] w {Location}",
                request.TemperatureCelsius,
                request.DeviceNameOrLocation);
        }

        return _mapper.Map<TemperatureLogDto>(entity);
    }

    /// <inheritdoc/>
    public async Task<HaccpReportDto> GetHaccpReportAsync(DateOnly from, DateOnly to)
    {
        var fromOffset = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var logs = (await _temperatureLogRepository.GetByDateRangeAsync(fromOffset, toOffset)).ToList();
        var dtos = _mapper.Map<List<TemperatureLogDto>>(logs);

        return new HaccpReportDto
        {
            DateFrom = from,
            DateTo = to,
            TotalReadings = dtos.Count,
            OutOfRangeReadings = dtos.Count(d => d.IsOutOfRange),
            Readings = dtos,
        };
    }
}
