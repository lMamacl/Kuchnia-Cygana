using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na kierowcach
/// </summary>

public interface IDriverRepository : IRepository<Driver>
{
    Task<IReadOnlyList<Driver>> GetByIdsAsync(IEnumerable<int> ids);
    Task<DriverSearchResult> SearchAsync(DriverSearchQuery query);
    Task<Driver?> GetByUserIdAsync(int userId);
    Task<Driver?> GetByLicenseNumberAsync(string licenseNumber);
    Task<IReadOnlyList<Driver>> GetByIdsAsync(IReadOnlyCollection<int> driverIds);
}

public sealed record DriverSearchQuery(
    string? Search,
    bool? IsActive,
    bool? HasVehicleAssignment,
    int Page,
    int PageSize);

public sealed class DriverSearchResult
{
    public IReadOnlyList<DriverListRow> Items { get; init; } = Array.Empty<DriverListRow>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalDriversCount { get; init; }

    public int ActiveCount { get; init; }

    public int WithVehicleCount { get; init; }

    public int InactiveCount { get; init; }
}

public sealed class DriverListRow
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string LicenseNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int? CurrentVehicleId { get; set; }

    public string? CurrentVehicleRegistration { get; set; }

    public string? CurrentVehicleModel { get; set; }
}
