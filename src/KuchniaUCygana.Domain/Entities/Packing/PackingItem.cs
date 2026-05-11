using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Pozycja paczki — pojedyncze pudełko z posiłkiem w torbie.
/// Powiązanie: Pudelko z class diagram.puml
/// </summary>
[Alias("PackingItems")]
public class PackingItem : AuditableEntity<int>
{
    [References(typeof(PackingSession))]
    public int PackingSessionId { get; set; }

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
    [References(typeof(Batch))]
    public int? BatchId { get; set; }

    /// <summary>
    /// Data ważności pudełka.
    /// </summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    /// <summary>
    /// Czy pudełko jest uszkodzone / zgłoszono brak.
    /// </summary>
    public bool IsDamaged { get; set; }

    /// <summary>
    /// Uwagi (np. przyczyna uszkodzenia).
    /// </summary>
    [StringLength(250)]
    public string? Remarks { get; set; }
}
