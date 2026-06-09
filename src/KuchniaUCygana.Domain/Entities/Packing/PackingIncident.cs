using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

[Table("PackingIncidents")]
public sealed class PackingIncident : AuditableEntity<int>
{
    public PackingIncidentType Type { get; set; }

    public PackingIncidentStatus Status { get; set; } = PackingIncidentStatus.New;

    public PackingIncidentReasonFlag ReasonFlags { get; set; } = PackingIncidentReasonFlag.None;

    public DateOnly PackingDate { get; set; }

    public int PackingSessionId { get; set; }

    public int? PackingItemId { get; set; }

    public int? PackingBagId { get; set; }

    public int? ReplacementPackingItemId { get; set; }

    public int? ReplacementPackingBagId { get; set; }

    [StringLength(50)]
    public string? ClientPublicId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int? MealId { get; set; }

    [StringLength(200)]
    public string? MealName { get; set; }

    [StringLength(100)]
    public string? BoxCode { get; set; }

    [StringLength(100)]
    public string? BagCode { get; set; }

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? ReportedByUserId { get; set; }

    public int? AssignedToUserId { get; set; }

    [StringLength(2000)]
    public string? AdminNotes { get; set; }

    public DateTimeOffset? WarehouseWasteRegisteredAt { get; set; }

    [StringLength(1000)]
    public string? WarehouseWasteError { get; set; }

    public DateTimeOffset? KitchenStartedAt { get; set; }

    public DateTimeOffset? KitchenPreparedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public int? ResolvedByUserId { get; set; }
}
