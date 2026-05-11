using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class AddressRepository : BaseRepository<Address>, IAddressRepository
{
    public AddressRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IEnumerable<Address>> GetByUserIdAsync(int userId) =>
        throw new NotImplementedException();

    public Task<Address?> GetDefaultByUserIdAsync(int userId) =>
        throw new NotImplementedException();

    public Task SetDefaultAsync(int userId, int addressId) =>
        throw new NotImplementedException();
}
