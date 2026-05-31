using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Pozycja paczki — pojedyncze pudełko z posiłkiem w torbie.
/// Powiązanie: Pudelko z class diagram.puml
/// </summary>
[Table("PackingItems")]
public class PackingItem : AuditableEntity<int>
{
    public int PackingSessionId { get; set; }

    /// <summary>
    /// Fizyczna torba, do ktorej przypisano pudelko.
    /// </summary>
    public int? PackingBagId { get; set; }

    /// <summary>
    /// Pozycja planu produkcji, z ktorej powstalo pudelko.
    /// </summary>
    public int? ProductionPlanItemId { get; set; }

    /// <summary>
    /// ID posiłku z Modułu 2 (bridge).
    /// </summary>
    public int MealId { get; set; }

    /// <summary>
    /// Nazwa posiłku (denormalizowana dla etykiet).
    /// </summary>
    [StringLength(200)]
    public string MealName { get; set; } = string.Empty;

    /// <summary>
    /// ID wariantu kalorycznego z Modułu 2 (bridge).
    /// </summary>
    public int DietVariantId { get; set; }

    /// <summary>
    /// Powiązanie z partią składnika (HACCP traceability).
    /// </summary>
    public int? BatchId { get; set; }

    [StringLength(100)]
    public string? BoxCode { get; set; }

    public PackingItemStatus Status { get; set; } = PackingItemStatus.Pending;

    /// <summary>
    /// Data ważności pudełka.
    /// </summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset? FoilPrintedAt { get; set; }

    public DateTimeOffset? PackedAt { get; set; }

    [StringLength(50)]
    public string? PackedBy { get; set; }

    /// <summary>
    /// Czy pudełko jest uszkodzone / zgłoszono brak.
    /// </summary>
    public bool IsDamaged { get; set; }

    public int? ReplacementForPackingItemId { get; set; }

    public DateTimeOffset? IssueReportedAt { get; set; }

    public int? IssueReportedByUserId { get; set; }

    /// <summary>
    /// Uwagi (np. przyczyna uszkodzenia).
    /// </summary>
    [StringLength(250)]
    public string? Remarks { get; set; }
}
