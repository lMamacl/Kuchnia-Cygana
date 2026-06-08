using KuchniaUCygana.Application.DTOs;

namespace KuchniaUCygana.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetByRolesAsync(IEnumerable<string> roles);

    Task<IReadOnlyList<UserDto>> GetByIdsAsync(IEnumerable<int> ids);

    Task<UserPageDto> SearchAsync(string? role, string? search, int page, int pageSize);

    Task<UserDirectorySummaryDto> GetDirectorySummaryAsync();
}
