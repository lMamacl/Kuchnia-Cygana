using System;
using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Alias("TemperatureLogs")]
public class TemperatureLog : AuditableEntity<long>
{
    [Required]
    [StringLength(50)]
    public string DeviceNameOrLocation { get; set; } = string.Empty;

    public decimal RecordedTemperatureCelsius { get; set; }

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Remarks { get; set; }
}
