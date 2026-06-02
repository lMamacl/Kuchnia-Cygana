namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class DriverDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
