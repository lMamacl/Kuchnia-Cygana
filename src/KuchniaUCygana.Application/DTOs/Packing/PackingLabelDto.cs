namespace KuchniaUCygana.Application.DTOs.Packing;

/// <summary>
/// DTO etykiety (produktowej lub wysyłkowej) — odpowiada PackingLabel.
/// </summary>
public sealed class PackingLabelDto
{
    public int Id { get; set; }

    public int? PackingItemId { get; set; }

    public int? PackingSessionId { get; set; }

    public int? PackingBagId { get; set; }

    public string LabelType { get; set; } = string.Empty;

    public string QrCode { get; set; } = string.Empty;

    // Etykieta produktowa
    public string? DishName { get; set; }

    public string? Allergens { get; set; }

    public int? Kcal { get; set; }

    // Etykieta wysyłkowa
    public string? ClientName { get; set; }

    public string? ClientPublicId { get; set; }

    public string? Address { get; set; }

    public string? BagCode { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int? StopNumber { get; set; }

    public string? VehicleRegistration { get; set; }

    public int TotalBoxes { get; set; }

    public int PackedBoxes { get; set; }

    public string? RouteInfo { get; set; }

    public string? DeliveryWindow { get; set; }

    public string? Ingredients { get; set; }

    // Rozszerzenia dla reprintu
    public string? MealsList { get; set; }

    public string? ReprintReason { get; set; }

    public int PrintNumber { get; set; } = 1;

    public DateTimeOffset? PrintedAt { get; set; }

    public string? PrintedBy { get; set; }

    public string? LabelDataJson { get; set; }
}
