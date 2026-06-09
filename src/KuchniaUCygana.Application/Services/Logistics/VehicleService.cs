using AutoMapper;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IDriverVehicleAssignmentRepository _assignmentRepository;
    private readonly IMapper _mapper;

    public VehicleService(
        IVehicleRepository vehicleRepo,
        IDriverVehicleAssignmentRepository assignmentRepository,
        IMapper mapper)
    {
        _vehicleRepo = vehicleRepo;
        _assignmentRepository = assignmentRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<VehicleDto>> GetAllAsync()
    {
        var vehicles = await _vehicleRepo.GetAllAsync();
        return _mapper.Map<IEnumerable<VehicleDto>>(vehicles);
    }

    public async Task<VehiclePageDto> SearchAsync(VehicleSearchRequest request)
    {
        var result = await _vehicleRepo.SearchAsync(new VehicleSearchQuery(
            request.Search,
            ParseVehicleStatus(request.Status),
            request.Page,
            request.PageSize));

        return new VehiclePageDto
        {
            Items = _mapper.Map<IReadOnlyList<VehicleDto>>(result.Items),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalFleetCount = result.TotalFleetCount,
            ActiveCount = result.ActiveCount,
            MaintenanceCount = result.MaintenanceCount,
            ActiveCapacityKg = result.ActiveCapacityKg,
        };
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
        var existingByRegistration = await _vehicleRepo.GetByRegistrationNumberAsync(request.RegistrationNumber);

        if (existingByRegistration != null && existingByRegistration.Id != id)
        {
            throw new InvalidOperationException($"Vehicle with registration number '{request.RegistrationNumber}' already exists.");
        }

        var existing = await _vehicleRepo.GetByIdAsync(id);
        if (existing == null) return null;
        _mapper.Map(request, existing);
        await _vehicleRepo.UpdateAsync(existing);
        if (!existing.IsOperational())
        {
            await _assignmentRepository.UnassignVehicleAsync(existing.Id);
        }

        return _mapper.Map<VehicleDto>(existing);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await _assignmentRepository.UnassignVehicleAsync(id);
        return await _vehicleRepo.DeleteAsync(id);
    }

    private static VehicleStatus? ParseVehicleStatus(string? status)
    {
        return Enum.TryParse<VehicleStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
