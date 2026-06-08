namespace KuchniaUCygana.Application.DTOs;

public sealed class UserDto
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

public sealed class UserDirectorySummaryDto
{
    public int TotalCount { get; init; }

    public int AdminCount { get; init; }

    public int StaffCount { get; init; }

    public int ClientCount { get; init; }

    public int ActiveRoleCount { get; init; }

    public IReadOnlyList<UserRoleSummaryDto> Roles { get; init; } = Array.Empty<UserRoleSummaryDto>();
}

public sealed class UserRoleSummaryDto
{
    public string Role { get; init; } = string.Empty;

    public int TotalCount { get; init; }

    public string SampleUsers { get; init; } = string.Empty;
}
