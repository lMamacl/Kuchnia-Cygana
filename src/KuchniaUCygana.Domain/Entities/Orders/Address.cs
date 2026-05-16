using KuchniaUCygana.Domain.Common;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Alias("Addresses")]
public sealed class Address : AuditableEntity
{
    public int UserId { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    public string BuildingNumber { get; set; } = string.Empty;

    public string? ApartmentNumber { get; set; }

    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public string? DeliveryNotes { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    // Pomocnicza właściwość — pełna linia adresu dla M3/logistyki.
    [Ignore]
    public string FullAddress =>
        string.IsNullOrWhiteSpace(ApartmentNumber)
            ? $"{Street} {BuildingNumber}, {PostalCode} {City}"
            : $"{Street} {BuildingNumber}/{ApartmentNumber}, {PostalCode} {City}";
}
