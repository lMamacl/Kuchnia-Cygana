using KuchniaUCygana.Domain.Entities.Auth;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByEmailAsync(string email);
    Task<bool> ExistsWithEmailAsync(string email);
}
