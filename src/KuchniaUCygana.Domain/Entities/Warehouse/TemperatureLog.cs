using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("TemperatureLogs")]
public class TemperatureLog : AuditableEntity<long>
{
    [Required]
    [StringLength(50)]
    public string DeviceNameOrLocation { get; set; } = string.Empty;

    public decimal RecordedTemperatureCelsius { get; set; }

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Remarks { get; set; }
}
