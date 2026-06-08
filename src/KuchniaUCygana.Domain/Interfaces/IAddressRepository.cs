using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IAddressRepository : IRepository<Address>
{
    Task<IReadOnlyList<Address>> GetByIdsAsync(IEnumerable<int> ids);
    Task<IEnumerable<Address>> GetByUserIdAsync(int userId);
    Task<Address?> GetDefaultByUserIdAsync(int userId);
    Task SetDefaultAsync(int userId, int addressId);
    Task<IEnumerable<Address>> GetPendingAddressesAsync();
}
