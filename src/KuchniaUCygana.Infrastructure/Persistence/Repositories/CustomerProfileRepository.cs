using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class CustomerProfileRepository : BaseRepository<CustomerProfile>, ICustomerProfileRepository
{
    public CustomerProfileRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<CustomerProfile?> GetByUserIdAsync(int userId) =>
        throw new NotImplementedException();
}
