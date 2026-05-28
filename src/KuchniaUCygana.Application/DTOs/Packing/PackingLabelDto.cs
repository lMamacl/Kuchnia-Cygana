namespace KuchniaUCygana.Application.DTOs.Packing;

/// <summary>
/// DTO etykiety (produktowej lub wysyłkowej) — odpowiada PackingLabel.
/// </summary>
public sealed class PackingLabelDto
{
    public int Id { get; set; }

    public int? PackingItemId { get; set; }

    public int? PackingSessionId { get; set; }

    public string LabelType { get; set; } = string.Empty;

    public string QrCode { get; set; } = string.Empty;

    // Etykieta produktowa
    public string? DishName { get; set; }

    public string? Allergens { get; set; }

    public int? Kcal { get; set; }

    // Etykieta wysyłkowa
    public string? ClientName { get; set; }

    public string? RouteInfo { get; set; }

    public string? DeliveryWindow { get; set; }

    public string? Ingredients { get; set; }

    // Rozszerzenia dla reprintu
    public string? MealsList { get; set; }

    public string? ReprintReason { get; set; }
}
