using KuchniaUCygana.Application.DTOs.Logistics;

namespace KuchniaUCygana.Application.Interfaces;

public interface IVehicleService
{
    Task<IEnumerable<VehicleDto>> GetAllAsync();
    Task<VehicleDto?> GetByIdAsync(int id);
    Task<VehicleDto> CreateAsync(CreateVehicleRequest request);
    Task<VehicleDto?> UpdateAsync(int id, UpdateVehicleRequest request);
    Task<bool> DeleteAsync(int id);
    Task<VehicleDto?> GetByRegistrationNumberAsync(string registrationNumber);
}
