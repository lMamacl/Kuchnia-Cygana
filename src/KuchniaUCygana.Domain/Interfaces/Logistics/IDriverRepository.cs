using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na kierowcach
/// </summary>

public interface IDriverRepository : IRepository<Driver>
{
    Task<IReadOnlyList<Driver>> GetByIdsAsync(IEnumerable<int> ids);
    Task<Driver?> GetByUserIdAsync(int userId);
    Task<Driver?> GetByLicenseNumberAsync(string licenseNumber);
}
