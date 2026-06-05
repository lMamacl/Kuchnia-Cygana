using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class CustomerProfileRepository : BaseRepository<CustomerProfile>, ICustomerProfileRepository
{
    public CustomerProfileRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    public async Task<CustomerProfile?> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM CustomerProfiles WHERE UserId = @UserId AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<CustomerProfile>(sql, new { UserId = userId });
    }
}


