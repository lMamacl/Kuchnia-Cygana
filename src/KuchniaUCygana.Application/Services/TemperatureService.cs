using AutoMapper;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class TemperatureService : ITemperatureService
{
    private const decimal DefaultMinTemperature = -25m;
    private const decimal DefaultMaxTemperature = 8m;

    private readonly ITemperatureLogRepository _temperatureLogRepository;
    private readonly IHaccpLocationRepository _haccpLocationRepository;
    private readonly IHaccpTemperatureAlertRepository _alertRepository;
    private readonly INotificationService _notificationService;
    private readonly IMapper _mapper;
    private readonly ILogger<TemperatureService> _logger;

    public TemperatureService(
        ITemperatureLogRepository temperatureLogRepository,
        IHaccpLocationRepository haccpLocationRepository,
        IHaccpTemperatureAlertRepository alertRepository,
        INotificationService notificationService,
        IMapper mapper,
        ILogger<TemperatureService> logger)
    {
        _temperatureLogRepository = temperatureLogRepository;
        _haccpLocationRepository = haccpLocationRepository;
        _alertRepository = alertRepository;
        _notificationService = notificationService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<TemperatureLogDto> LogTemperatureAsync(LogTemperatureRequest request)
    {
        var location = await ResolveLocationAsync(request);
        var deviceName = location?.Name ?? request.DeviceNameOrLocation.Trim();

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new InvalidOperationException("Wybierz lokalizację HACCP.");
        }

        var entity = new TemperatureLog
        {
            HaccpLocationId = location?.Id,
            DeviceNameOrLocation = deviceName,
            RecordedTemperatureCelsius = request.TemperatureCelsius,
            RecordedAt = DateTimeOffset.UtcNow,
            Remarks = request.Remarks,
        };

        var id = await _temperatureLogRepository.InsertAsync(entity);
        entity.Id = id;

        if (location is not null)
        {
            await HandleTemperatureAlertAsync(entity, location);
        }

        return MapTemperatureLog(entity, location);
    }

    public async Task<HaccpReportDto> GetHaccpReportAsync(DateOnly from, DateOnly to)
    {
        var fromOffset = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var locations = await GetLocationLookupAsync(activeOnly: false);
        var activeLocations = await GetActiveLocationsAsync();
        var logs = (await _temperatureLogRepository.GetByDateRangeAsync(fromOffset, toOffset)).ToList();
        var dtos = logs
            .Select(log => MapTemperatureLog(log, ResolveLocationForLog(log, locations)))
            .ToList();

        return new HaccpReportDto
        {
            DateFrom = from,
            DateTo = to,
            TotalReadings = dtos.Count,
            OutOfRangeReadings = dtos.Count(d => d.IsOutOfRange),
            Readings = dtos,
            Locations = activeLocations,
        };
    }

    public async Task<IReadOnlyList<HaccpLocationDto>> GetActiveLocationsAsync()
    {
        var rows = await _haccpLocationRepository.GetAllWithCategoriesAsync(activeOnly: true);
        return rows.Select(MapLocation).ToList();
    }

    public async Task<IEnumerable<TemperatureChartDataDto>> GetChartDataAsync(string device, int days)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days, 1, 90));
        var now = DateTimeOffset.UtcNow;
        var locations = await GetLocationLookupAsync(activeOnly: false);
        IEnumerable<TemperatureLog> logs;

        var location = locations.Values.FirstOrDefault(l =>
            string.Equals(l.Name, device, StringComparison.OrdinalIgnoreCase)
            || string.Equals(l.Code, device, StringComparison.OrdinalIgnoreCase));

        if (location is not null)
        {
            logs = await _temperatureLogRepository.GetByLocationIdDateRangeAsync(location.Id, cutoff, now);
        }
        else if (string.IsNullOrWhiteSpace(device))
        {
            logs = await _temperatureLogRepository.GetByDateRangeAsync(cutoff, now);
        }
        else
        {
            logs = (await _temperatureLogRepository.GetByLocationAsync(device))
                .Where(l => l.RecordedAt >= cutoff);
        }

        return logs
            .OrderBy(l => l.RecordedAt)
            .Select(l =>
            {
                var logLocation = ResolveLocationForLog(l, locations);
                var dto = MapTemperatureLog(l, logLocation);
                return new TemperatureChartDataDto
                {
                    RecordedAt = dto.RecordedAt,
                    Temperature = dto.RecordedTemperatureCelsius,
                    DeviceName = dto.DeviceNameOrLocation,
                    IsAlert = dto.IsOutOfRange,
                };
            })
            .ToList();
    }

    public async Task<string> ExportHaccpCsvAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var locations = await GetLocationLookupAsync(activeOnly: false);
        var logs = (await _temperatureLogRepository.GetByDateRangeAsync(from, to))
            .OrderBy(l => l.RecordedAt)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,RecordedAt,DeviceNameOrLocation,TemperatureCelsius,MinLimit,MaxLimit,IsAlert,Remarks");

        foreach (var log in logs)
        {
            var dto = MapTemperatureLog(log, ResolveLocationForLog(log, locations));
            var deviceName = dto.DeviceNameOrLocation.Replace(",", ";", StringComparison.OrdinalIgnoreCase);
            var remarks = (dto.Remarks ?? string.Empty).Replace(",", ";", StringComparison.OrdinalIgnoreCase);

            sb.AppendLine($"{dto.Id},{dto.RecordedAt:yyyy-MM-dd HH:mm:ss},{deviceName},{dto.RecordedTemperatureCelsius},{dto.MinTemperatureCelsius},{dto.MaxTemperatureCelsius},{dto.IsOutOfRange},{remarks}");
        }

        return sb.ToString();
    }

    private async Task<HaccpLocationDto?> ResolveLocationAsync(LogTemperatureRequest request)
    {
        if (request.HaccpLocationId.HasValue)
        {
            var row = await _haccpLocationRepository.GetDetailsByIdAsync(request.HaccpLocationId.Value);
            if (row is null || !row.IsActive)
            {
                throw new InvalidOperationException("Wybrana lokalizacja HACCP nie istnieje albo jest nieaktywna.");
            }

            return MapLocation(row);
        }

        if (string.IsNullOrWhiteSpace(request.DeviceNameOrLocation))
        {
            return null;
        }

        var locations = await GetActiveLocationsAsync();
        return locations.FirstOrDefault(location =>
            string.Equals(location.Name, request.DeviceNameOrLocation.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task HandleTemperatureAlertAsync(TemperatureLog log, HaccpLocationDto location)
    {
        var isOutOfRange = IsOutOfRange(
            log.RecordedTemperatureCelsius,
            location.MinTemperatureCelsius,
            location.MaxTemperatureCelsius);
        var openAlert = await _alertRepository.GetOpenByLocationAsync(location.Id);

        if (!isOutOfRange)
        {
            if (openAlert is not null)
            {
                openAlert.Status = HaccpTemperatureAlertStatus.Closed;
                openAlert.ClosedAt = log.RecordedAt;
                openAlert.LastObservedAt = log.RecordedAt;
                openAlert.LastTemperatureCelsius = log.RecordedTemperatureCelsius;
                openAlert.Message = $"Temperatura wróciła do normy w lokalizacji {location.Name}.";
                await _alertRepository.UpdateAsync(openAlert);
            }

            return;
        }

        if (openAlert is not null)
        {
            openAlert.LastObservedAt = log.RecordedAt;
            openAlert.LastTemperatureCelsius = log.RecordedTemperatureCelsius;
            openAlert.Message = BuildAlertMessage(log.RecordedTemperatureCelsius, location);
            await _alertRepository.UpdateAsync(openAlert);
            return;
        }

        var alert = new HaccpTemperatureAlert
        {
            HaccpLocationId = location.Id,
            TemperatureLogId = log.Id,
            Status = HaccpTemperatureAlertStatus.Open,
            TriggeredTemperatureCelsius = log.RecordedTemperatureCelsius,
            LastTemperatureCelsius = log.RecordedTemperatureCelsius,
            MinTemperatureCelsius = location.MinTemperatureCelsius,
            MaxTemperatureCelsius = location.MaxTemperatureCelsius,
            OpenedAt = log.RecordedAt,
            LastObservedAt = log.RecordedAt,
            Message = BuildAlertMessage(log.RecordedTemperatureCelsius, location),
        };

        alert.Id = await _alertRepository.InsertAsync(alert);

        await _notificationService.CreateForRolesAsync(
            new Notification
            {
                Type = "HaccpTemperature",
                Severity = NotificationSeverity.Danger,
                Title = "Alert temperatury HACCP",
                Message = alert.Message,
                LinkUrl = $"/warehouse/haccp-report?activeTab={Uri.EscapeDataString(location.Name)}",
                DeduplicationKey = $"haccp-temperature-alert-{alert.Id}",
                SourceType = nameof(HaccpTemperatureAlert),
                SourceId = alert.Id,
            },
            new[] { UserRoles.WarehouseManager, UserRoles.Admin });

        _logger.LogWarning(
            "HACCP ALERT: Temperatura {Temp}°C poza zakresem {Min}°C - {Max}°C w {Location}",
            log.RecordedTemperatureCelsius,
            location.MinTemperatureCelsius,
            location.MaxTemperatureCelsius,
            location.Name);
    }

    private async Task<Dictionary<int, HaccpLocationDto>> GetLocationLookupAsync(bool activeOnly)
    {
        var rows = await _haccpLocationRepository.GetAllWithCategoriesAsync(activeOnly);
        return rows.Select(MapLocation).ToDictionary(location => location.Id);
    }

    private static HaccpLocationDto? ResolveLocationForLog(
        TemperatureLog log,
        IReadOnlyDictionary<int, HaccpLocationDto> locations)
    {
        if (log.HaccpLocationId.HasValue
            && locations.TryGetValue(log.HaccpLocationId.Value, out var location))
        {
            return location;
        }

        return locations.Values.FirstOrDefault(location =>
            string.Equals(location.Name, log.DeviceNameOrLocation, StringComparison.OrdinalIgnoreCase));
    }

    private static TemperatureLogDto MapTemperatureLog(TemperatureLog log, HaccpLocationDto? location)
    {
        var min = location?.MinTemperatureCelsius ?? DefaultMinTemperature;
        var max = location?.MaxTemperatureCelsius ?? DefaultMaxTemperature;
        var dto = new TemperatureLogDto
        {
            Id = log.Id,
            HaccpLocationId = log.HaccpLocationId,
            DeviceNameOrLocation = location?.Name ?? log.DeviceNameOrLocation,
            RecordedTemperatureCelsius = log.RecordedTemperatureCelsius,
            RecordedAt = log.RecordedAt,
            Remarks = log.Remarks,
            MinTemperatureCelsius = min,
            MaxTemperatureCelsius = max,
            IsOutOfRange = IsOutOfRange(log.RecordedTemperatureCelsius, min, max),
        };

        return dto;
    }

    private static HaccpLocationDto MapLocation(HaccpLocationDetailsRow row)
    {
        return new HaccpLocationDto
        {
            Id = row.Id,
            Code = row.Code,
            Name = row.Name,
            MinTemperatureCelsius = row.MinTemperatureCelsius,
            MaxTemperatureCelsius = row.MaxTemperatureCelsius,
            IsActive = row.IsActive,
            DisplayOrder = row.DisplayOrder,
            Notes = row.Notes,
            CategoryIds = ParseIds(row.CategoryIds),
            CategoryNames = row.CategoryNames,
        };
    }

    private static IReadOnlyList<int> ParseIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<int>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }

    private static bool IsOutOfRange(decimal temperature, decimal min, decimal max)
    {
        return temperature < min || temperature > max;
    }

    private static string BuildAlertMessage(decimal temperature, HaccpLocationDto location)
    {
        return $"Temperatura {temperature:F1}°C poza zakresem {location.MinTemperatureCelsius:F1}°C - {location.MaxTemperatureCelsius:F1}°C w lokalizacji {location.Name}.";
    }
}
