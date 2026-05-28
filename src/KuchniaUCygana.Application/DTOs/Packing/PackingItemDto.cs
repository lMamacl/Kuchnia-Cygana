namespace KuchniaUCygana.Application.DTOs.Packing;

/// <summary>
/// DTO pozycji pakowania — odpowiada PackingItem.
/// </summary>
public sealed class PackingItemDto
{
    public int Id { get; set; }

    public int PackingSessionId { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public int? BatchId { get; set; }

    public string? BoxCode { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset? FoilPrintedAt { get; set; }

    public DateTimeOffset? PackedAt { get; set; }

    public string? PackedBy { get; set; }

    public bool IsDamaged { get; set; }

    public string? Remarks { get; set; }
}
