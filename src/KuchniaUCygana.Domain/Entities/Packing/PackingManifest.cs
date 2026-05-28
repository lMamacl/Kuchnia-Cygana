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

    [Required]
    public string PayloadJson { get; set; } = string.Empty;
}
