namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ReprintLabelRequest
{
    public int? PackingSessionId { get; set; }

    public int? PackingBagId { get; set; }

    public int? PackingItemId { get; set; }

    public string ReprintReason { get; set; } = string.Empty;
}
