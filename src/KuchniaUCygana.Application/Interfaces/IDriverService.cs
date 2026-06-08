using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Application.Interfaces;

public interface IDriverService
{
    Task<IEnumerable<DriverDto>> GetAllAsync();
    Task<DriverPageDto> SearchAsync(DriverSearchRequest request);
    Task<DriverDto?> GetByIdAsync(int id);
    Task<IReadOnlyList<DriverUserOptionDto>> GetAssignableUsersAsync();
    Task<DriverDto> CreateAsync(CreateDriverRequest request);
    Task<DriverDto?> UpdateAsync(int id, UpdateDriverRequest request);
    Task<DriverDto?> AssignVehicleAsync(int driverId, int vehicleId);
    Task<bool> UnassignVehicleAsync(int driverId);
    Task<bool> DeleteAsync(int id);
}
