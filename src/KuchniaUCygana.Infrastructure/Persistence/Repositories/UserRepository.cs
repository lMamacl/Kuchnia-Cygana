using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<User?> FindByEmailAsync(string email)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleAsync<User>(x => x.Email == email);
    }

    public async Task<bool> ExistsWithEmailAsync(string email)
    {
        using var db = Factory.CreateConnection();
        return await db.ExistsAsync<User>(x => x.Email == email);
    }
}
