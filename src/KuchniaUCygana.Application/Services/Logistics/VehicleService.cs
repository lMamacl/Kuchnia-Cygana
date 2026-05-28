using AutoMapper;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IMapper _mapper;

    public VehicleService(IVehicleRepository vehicleRepo, IMapper mapper)
    {
        _vehicleRepo = vehicleRepo;
        _mapper = mapper;
    }

    public async Task<IEnumerable<VehicleDto>> GetAllAsync()
    {
        var vehicles = await _vehicleRepo.GetAllAsync();
        return _mapper.Map<IEnumerable<VehicleDto>>(vehicles);
    }

    public async Task<VehicleDto?> GetByIdAsync(int id)
    {
        var vehicle = await _vehicleRepo.GetByIdAsync(id);
        return vehicle == null ? null : _mapper.Map<VehicleDto>(vehicle);
    }

    public async Task<VehicleDto?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        var vehicle = await _vehicleRepo.GetByRegistrationNumberAsync(registrationNumber);
        return vehicle == null ? null : _mapper.Map<VehicleDto>(vehicle);
    }

    public async Task<VehicleDto> CreateAsync(CreateVehicleRequest request)
    {
        var existing = await _vehicleRepo.GetByRegistrationNumberAsync(request.RegistrationNumber);

        if (existing != null)
            throw new InvalidOperationException($"Vehicle with registration number '{request.RegistrationNumber}' already exists.");

        var vehicle = _mapper.Map<Vehicle>(request);
        var id = await _vehicleRepo.InsertAsync(vehicle);
        var created = await _vehicleRepo.GetByIdAsync(id);
        return _mapper.Map<VehicleDto>(created);
    }

    public async Task<VehicleDto?> UpdateAsync(int id, UpdateVehicleRequest request)
    {
        var existing = await _vehicleRepo.GetByIdAsync(id);
        if (existing == null) return null;
        _mapper.Map(request, existing);
        await _vehicleRepo.UpdateAsync(existing);
        return _mapper.Map<VehicleDto>(existing);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _vehicleRepo.DeleteAsync(id);
    }
}
