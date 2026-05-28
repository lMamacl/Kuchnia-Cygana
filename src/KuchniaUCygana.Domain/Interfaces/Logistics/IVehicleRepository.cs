using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Definicja operacji bazodanowych na pojazdach
/// </summary>

public interface IVehicleRepository : IRepository<Vehicle>
{
    public Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber);
}
