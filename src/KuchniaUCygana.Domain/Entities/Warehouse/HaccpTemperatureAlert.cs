using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("HaccpTemperatureAlerts")]
public sealed class HaccpTemperatureAlert : BaseEntity<long>
{
    public int HaccpLocationId { get; set; }

    public long TemperatureLogId { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = HaccpTemperatureAlertStatus.Open;

    public decimal TriggeredTemperatureCelsius { get; set; }

    public decimal LastTemperatureCelsius { get; set; }

    public decimal MinTemperatureCelsius { get; set; }

    public decimal MaxTemperatureCelsius { get; set; }

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastObservedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ClosedAt { get; set; }

    [Required]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;
}

public static class HaccpTemperatureAlertStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";
}
