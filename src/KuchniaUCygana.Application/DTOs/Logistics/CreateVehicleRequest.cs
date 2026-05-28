using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class CreateVehicleRequest
{
    public required string RegistrationNumber { get; init; } = string.Empty;
    public required string Model { get; init; } = string.Empty;
    public required decimal MaxLoadKg { get; init; }
    public VehicleStatus Status { get; init; } = VehicleStatus.Active;
}