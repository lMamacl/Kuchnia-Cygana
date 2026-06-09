using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na pojazdach
/// </summary>

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IEnumerable<int> ids);
    Task<IReadOnlyList<Vehicle>> GetActiveAsync();
    Task<VehicleSearchResult> SearchAsync(VehicleSearchQuery query);
    public Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber);
    Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IReadOnlyCollection<int> vehicleIds);
}

public sealed record VehicleSearchQuery(
    string? Search,
    VehicleStatus? Status,
    int Page,
    int PageSize);

public sealed class VehicleSearchResult
{
    public IReadOnlyList<Vehicle> Items { get; init; } = Array.Empty<Vehicle>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalFleetCount { get; init; }

    public int ActiveCount { get; init; }

    public int MaintenanceCount { get; init; }

    public decimal ActiveCapacityKg { get; init; }
}
