namespace KuchniaUCygana.Application.DTOs.Packing;

/// <summary>
/// DTO sesji pakowania z listą pozycji — odpowiada PackingSession + Items.
/// </summary>
public sealed class PackingSessionDto
{
    public int Id { get; set; }

    public DateOnly PackingDate { get; set; }

    public int? OrderId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public string? ClientName { get; set; }

    public string? ClientPublicId { get; set; }

    public string ClientPublicIdDisplay { get; set; } = string.Empty;

    public string? PackedBy { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool CanScan { get; set; }

    public bool CanCloseBag { get; set; }

    public string? BlockReason { get; set; }

    public int? RouteId { get; set; }

    public int? StopNumber { get; set; }

    public List<PackingItemDto> Items { get; set; } = new();
}
