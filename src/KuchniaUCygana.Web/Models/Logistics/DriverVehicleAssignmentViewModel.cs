using System.ComponentModel.DataAnnotations;
using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Web.Models.Logistics;

public sealed class DriverVehicleAssignmentViewModel
{
    public DriverDto Driver { get; set; } = new();

    [Range(1, int.MaxValue, ErrorMessage = "Wybierz pojazd.")]
    public int VehicleId { get; set; }

    public IReadOnlyList<DriverVehicleOptionViewModel> Vehicles { get; set; } = [];
}

public sealed class DriverVehicleOptionViewModel
{
    public int VehicleId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsCurrentForDriver { get; set; }
    public string? AssignedDriverName { get; set; }
}
