using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Problem zgloszony przez kierowce podczas realizacji dostawy.
/// </summary>
[Table("DeliveryIssues")]
public sealed class DeliveryIssue : AuditableEntity
{
    public int RouteStopId { get; set; }

    public int DriverId { get; set; }

    [StringLength(100)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset ReportedAt { get; set; }
}
