namespace KuchniaUCygana.Application.DTOs.Production;

public sealed class PackingManifestDto
{
    public int Id { get; set; }

    public DateOnly PackingDate { get; set; }

    public string ManifestNumber { get; set; } = string.Empty;

    public int? RouteId { get; set; }

    public string? RouteName { get; set; }

    public int? VehicleId { get; set; }

    public string? VehicleRegistration { get; set; }

    public int RouteCount { get; set; }

    public int BagCount { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public string? GeneratedBy { get; set; }

    public bool IsVerified { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public string? VerifiedBy { get; set; }

    public string PayloadJson { get; set; } = string.Empty;
}
