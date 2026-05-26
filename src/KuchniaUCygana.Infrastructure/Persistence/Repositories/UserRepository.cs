using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<User?> FindByEmailAsync(string email)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Users WHERE Email = @Email";
        return await db.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<bool> ExistsWithEmailAsync(string email)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        var count = await db.ExecuteScalarAsync<int>(sql, new { Email = email });
        return count > 0;
    }
}
