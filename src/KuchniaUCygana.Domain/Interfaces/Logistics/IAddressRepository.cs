using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces;

/// <summary>
/// Definicja operacji bazodanowych na adresach
/// </summary>

public interface IAddressRepository : IRepository<Address>
{
    Task<IEnumerable<Address>> GetNonGeocodedAddressesAsync();
}