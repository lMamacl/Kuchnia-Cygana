namespace KuchniaUCygana.Application.DTOs;

public sealed class UserPageDto
{
    public IReadOnlyList<UserDto> Items { get; init; } = Array.Empty<UserDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
