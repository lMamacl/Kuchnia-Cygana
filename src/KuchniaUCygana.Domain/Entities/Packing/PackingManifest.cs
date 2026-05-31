using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Trwaly snapshot manifestu pakowania/zaladunku dla kierowcy i magazynu.
/// </summary>
[Table("PackingManifests")]
public sealed class PackingManifest : BaseEntity<int>
{
    public DateOnly PackingDate { get; set; }

    [Required]
    [StringLength(50)]
    public string ManifestNumber { get; set; } = string.Empty;

    public int? RouteId { get; set; }

    [StringLength(150)]
    public string? RouteName { get; set; }

    public int? VehicleId { get; set; }

    [StringLength(50)]
    public string? VehicleRegistration { get; set; }

    public int RouteCount { get; set; }

    public int BagCount { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    [StringLength(100)]
    public string? GeneratedBy { get; set; }

    public bool IsVerified { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    [StringLength(100)]
    public string? VerifiedBy { get; set; }

    public int? VerifiedByUserId { get; set; }

    public DateTimeOffset? WorkerApprovedAt { get; set; }

    [StringLength(100)]
    public string? WorkerApprovedBy { get; set; }

    public int? WorkerApprovedByUserId { get; set; }

    public DateTimeOffset? SentToLogisticsAt { get; set; }

    public int? SentToLogisticsByUserId { get; set; }

    public bool RequiresRegeneration { get; set; }

    [StringLength(500)]
    public string? RequiresRegenerationReason { get; set; }

    [StringLength(128)]
    public string? SnapshotHash { get; set; }

    public int? DriverUserId { get; set; }

    public int ManifestVersion { get; set; } = 1;

    public int? SupersedesManifestId { get; set; }

    [StringLength(250)]
    public string? ChangeReason { get; set; }

    public bool IsSuperseded { get; set; }

    [Required]
    public string PayloadJson { get; set; } = string.Empty;
}
