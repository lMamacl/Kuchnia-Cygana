using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class TemperatureChartDataDto
{
    public DateTimeOffset RecordedAt { get; set; }

    public decimal Temperature { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public bool IsAlert { get; set; }
}
