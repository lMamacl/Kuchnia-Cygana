namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}
