namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class UpdateAddressRequest
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string BuildingNumber { get; set; } = string.Empty;
    public string? ApartmentNumber { get; set; }
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string? DeliveryNotes { get; set; }
}
