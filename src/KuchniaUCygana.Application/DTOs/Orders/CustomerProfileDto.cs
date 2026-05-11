namespace KuchniaUCygana.Application.DTOs.Orders;

public sealed class CustomerProfileDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Phone { get; set; }
    public string? DietaryNotes { get; set; }
    public int? DefaultAddressId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<AddressDto> Addresses { get; set; } = new();
}

public sealed class UpdateCustomerProfileRequest
{
    public string? Phone { get; set; }
    public string? DietaryNotes { get; set; }
}
