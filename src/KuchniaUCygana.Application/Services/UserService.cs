using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Constants;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository repository;

    public UserService(IUserRepository repository)
    {
        this.repository = repository;
    }

    public async Task<IReadOnlyList<UserDto>> GetByRolesAsync(IEnumerable<string> roles)
    {
        var users = await this.repository.GetByRolesAsync(roles);
        return users.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<UserDto>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var users = await this.repository.GetByIdsAsync(ids);
        return users.Select(Map).ToArray();
    }

    public async Task<UserPageDto> SearchAsync(string? role, string? search, int page, int pageSize)
    {
        var result = await this.repository.SearchAsync(new UserSearchQuery(role, search, page, pageSize));
        return new UserPageDto
        {
            Items = result.Items.Select(Map).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<UserDirectorySummaryDto> GetDirectorySummaryAsync()
    {
        var roles = (await this.repository.GetRoleSummariesAsync())
            .Select(row => new UserRoleSummaryDto
            {
                Role = row.Role,
                TotalCount = row.TotalCount,
                SampleUsers = row.SampleUsers,
            })
            .ToArray();

        return new UserDirectorySummaryDto
        {
            TotalCount = roles.Sum(role => role.TotalCount),
            AdminCount = roles
                .Where(role => string.Equals(role.Role, AppRoles.Admin, StringComparison.Ordinal))
                .Sum(role => role.TotalCount),
            StaffCount = roles
                .Where(role => AppRoles.IsStaffRole(role.Role))
                .Sum(role => role.TotalCount),
            ClientCount = roles
                .Where(role => string.Equals(role.Role, UserRoles.Client, StringComparison.Ordinal))
                .Sum(role => role.TotalCount),
            ActiveRoleCount = roles.Count(role => role.TotalCount > 0),
            Roles = roles,
        };
    }

    private static UserDto Map(User user)
        => new()
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = user.Role,
        };
}
