namespace KuchniaUCygana.Application.DTOs.Admin;

public sealed class SystemLogPageDto
{
    public IReadOnlyList<SystemLogDto> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public int TotalPages { get; init; } = 1;

    public IReadOnlyList<string> Actions { get; init; } = [];

    public IReadOnlyList<string> TargetEntities { get; init; } = [];
}
