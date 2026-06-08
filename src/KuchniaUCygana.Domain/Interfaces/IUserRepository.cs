using KuchniaUCygana.Domain.Entities.Auth;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByEmailAsync(string email);
    Task<bool> ExistsWithEmailAsync(string email);
    Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<int> ids);
    Task<IReadOnlyList<User>> GetByRolesAsync(IEnumerable<string> roles);
    Task<User?> GetFirstByRoleAsync(string role);
    Task<UserSearchResult> SearchAsync(UserSearchQuery query);
    Task<IReadOnlyList<UserRoleSummaryRow>> GetRoleSummariesAsync();
}

public sealed record UserSearchQuery(
    string? Role,
    string? Search,
    int Page,
    int PageSize);

public sealed class UserSearchResult
{
    public IReadOnlyList<User> Items { get; init; } = Array.Empty<User>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class UserRoleSummaryRow
{
    public string Role { get; init; } = string.Empty;

    public int TotalCount { get; init; }

    public string SampleUsers { get; init; } = string.Empty;
}
