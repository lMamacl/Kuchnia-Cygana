using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class AddressRepository : BaseRepository<Address>, IAddressRepository
{
    public AddressRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Addresses WHERE UserId = @UserId AND IsDeleted = 0 ORDER BY IsDefault DESC, Label ASC";
        return await db.QueryAsync<Address>(sql, new { UserId = userId });
    }

    public async Task<Address?> GetDefaultByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Addresses WHERE UserId = @UserId AND IsDefault = 1 AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<Address>(sql, new { UserId = userId });
    }

    public async Task SetDefaultAsync(int userId, int addressId)
    {
        using var db = Factory.CreateConnection();
        var now = DateTimeOffset.UtcNow;

        const string resetSql = "UPDATE Addresses SET IsDefault = 0, UpdatedAt = @Now WHERE UserId = @UserId AND IsDefault = 1 AND IsDeleted = 0";
        await db.ExecuteAsync(resetSql, new { UserId = userId, Now = now });

        const string setSql = "UPDATE Addresses SET IsDefault = 1, UpdatedAt = @Now WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";
        await db.ExecuteAsync(setSql, new { Id = addressId, UserId = userId, Now = now });
    }
}
