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

    public async Task<IReadOnlyList<User>> GetByRolesAsync(IEnumerable<string> roles)
    {
        using var db = Factory.CreateConnection();
        var roleList = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roleList.Length == 0)
        {
            return Array.Empty<User>();
        }

        const string sql = """
            SELECT *
            FROM [Users]
            WHERE [Role] IN @Roles
            ORDER BY [Role], [LastName], [FirstName], [Id];
            """;

        var users = await db.QueryAsync<User>(sql, new { Roles = roleList });
        return users.ToList();
    }

    public async Task<User?> GetFirstByRoleAsync(string role)
    {
        using var db = Factory.CreateConnection();
        const string sql = """
            SELECT TOP 1 *
            FROM [Users]
            WHERE [Role] = @Role
            ORDER BY [Id];
            """;

        return await db.QuerySingleOrDefaultAsync<User>(sql, new { Role = role });
    }
}
