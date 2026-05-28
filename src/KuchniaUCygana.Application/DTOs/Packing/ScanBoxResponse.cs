namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class ScanBoxResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int PackingSessionId { get; set; }

    public int PackingItemId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public string BoxCode { get; set; } = string.Empty;

    public bool AllBoxesPacked { get; set; }

    public string ClientName { get; set; } = string.Empty;
}
