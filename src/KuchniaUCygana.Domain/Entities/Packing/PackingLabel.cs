using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Packing;

/// <summary>
/// Etykieta — produktowa (na pudełko) lub wysyłkowa (na torbę).
/// Powiązanie: EtykietaProduktowa + EtykietaWysylkowa z class diagram.puml
/// </summary>
[Table("PackingLabels")]
public class PackingLabel : BaseEntity<int>
{
    /// <summary>
    /// Powiązanie z pudełkiem (etykieta produktowa) lub sesją (wysyłkowa).
    /// </summary>
    public int? PackingItemId { get; set; }

    public int? PackingSessionId { get; set; }

    /// <summary>
    /// Fizyczna torba transportowa. Dla nowych etykiet transportowych jest to glowny klucz.
    /// </summary>
    public int? PackingBagId { get; set; }

    /// <summary>
    /// Typ etykiety: Product (na pudełko) lub Shipping (na torbę).
    /// </summary>
    public LabelType LabelType { get; set; }

    /// <summary>
    /// Kod QR (wygenerowany UUID lub sekwencyjny).
    /// </summary>
    [Required]
    [StringLength(300)]
    public string QrCode { get; set; } = string.Empty;

    /// <summary>
    /// Nazwa dania (etykieta produktowa).
    /// </summary>
    [StringLength(200)]
    public string? DishName { get; set; }

    /// <summary>
    /// Lista alergenów (etykieta produktowa).
    /// </summary>
    [StringLength(500)]
    public string? Allergens { get; set; }

    /// <summary>
    /// Kaloryczność (etykieta produktowa).
    /// </summary>
    public int? Kcal { get; set; }

    /// <summary>
    /// Nazwa klienta (etykieta wysyłkowa).
    /// </summary>
    [StringLength(150)]
    public string? ClientName { get; set; }

    /// <summary>
    /// Informacje o trasie (etykieta wysyłkowa).
    /// </summary>
    [StringLength(200)]
    public string? RouteInfo { get; set; }

    /// <summary>
    /// Okno dostawy (etykieta wysyłkowa).
    /// </summary>
    [StringLength(50)]
    public string? DeliveryWindow { get; set; }

    [StringLength(2000)]
    public string? MealsList { get; set; }

    [StringLength(250)]
    public string? ReprintReason { get; set; }

    public int PrintNumber { get; set; } = 1;

    public string? LabelDataJson { get; set; }

    public DateTimeOffset? PrintedAt { get; set; }

    [StringLength(100)]
    public string? PrintedBy { get; set; }
}

/// <summary>
/// Typ etykiety.
/// </summary>
public enum LabelType
{
    Product = 0,    // Etykieta produktowa na pudełko (danie, kcal, alergeny)
    Shipping = 1    // Etykieta wysyłkowa na torbę (trasa, stop, klient)
}
