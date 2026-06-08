using KuchniaUCygana.Domain.Entities.Auth;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByEmailAsync(string email);
    Task<bool> ExistsWithEmailAsync(string email);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<int> userIds);
    Task<IReadOnlyList<User>> GetByRolesAsync(IEnumerable<string> roles);
    Task<User?> GetFirstByRoleAsync(string role);
}
