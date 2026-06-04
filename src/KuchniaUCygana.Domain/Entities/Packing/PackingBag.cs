using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Fizyczna torba transportowa. Jedna logiczna dostawa (PackingSession/DeliveryCalendarId)
/// moze miec wiele takich toreb.
/// </summary>
[Table("PackingBags")]
public sealed class PackingBag : AuditableEntity<int>
{
    public int PackingSessionId { get; set; }

    public int BagNumber { get; set; }

    [StringLength(100)]
    public string BagCode { get; set; } = string.Empty;

    public PackingBagStatus Status { get; set; } = PackingBagStatus.Pending;

    public DateTimeOffset? PackedAt { get; set; }

    [StringLength(50)]
    public string? PackedBy { get; set; }

    public DateTimeOffset? LabeledAt { get; set; }

    public DateTimeOffset? ManifestedAt { get; set; }

    public DateTimeOffset? LoadedAt { get; set; }

    [StringLength(50)]
    public string? LoadedBy { get; set; }

    public DateTimeOffset? DispatchedAt { get; set; }

    [StringLength(500)]
    public string? DamageReason { get; set; }

    public DateTimeOffset? DamagedAt { get; set; }

    public int? DamagedByUserId { get; set; }

    public int? ReplacementPackingBagId { get; set; }
}
