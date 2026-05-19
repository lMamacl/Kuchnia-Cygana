using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class CustomerProfileRepository : BaseRepository<CustomerProfile>, ICustomerProfileRepository
{
    public CustomerProfileRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<CustomerProfile?> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleAsync<CustomerProfile>(x =>
            x.UserId == userId && x.IsDeleted == false);
    }
}
