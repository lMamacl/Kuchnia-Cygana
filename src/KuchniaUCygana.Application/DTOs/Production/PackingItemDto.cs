namespace KuchniaUCygana.Application.DTOs.Production;

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

    public DateTimeOffset? ExpiryDate { get; set; }

    public bool IsDamaged { get; set; }

    public string? Remarks { get; set; }
}
