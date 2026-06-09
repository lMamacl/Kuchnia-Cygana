using KuchniaUCygana.Domain.Entities.Logistics;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

public interface IDriverVehicleAssignmentRepository
{
    Task<IReadOnlyList<DriverVehicleAssignment>> GetActiveAsync();
    Task<DriverVehicleAssignment?> GetActiveByDriverIdAsync(int driverId);
    Task<DriverVehicleAssignment?> GetActiveByVehicleIdAsync(int vehicleId);
    Task<IReadOnlyList<DriverVehicleAssignment>> GetActiveByVehicleIdsAsync(IReadOnlyCollection<int> vehicleIds);
    Task AssignAsync(int driverId, int vehicleId);
    Task<bool> UnassignDriverAsync(int driverId);
    Task<bool> UnassignVehicleAsync(int vehicleId);
}
