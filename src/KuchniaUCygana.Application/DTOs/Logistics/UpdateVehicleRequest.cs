namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class UpdateVehicleRequest
{
    public required int Id { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? Model { get; init; }
    public decimal? MaxLoadKg { get; init; }
}