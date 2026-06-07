namespace KuchniaUCygana.Web.Models;

public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public int TotalCount { get; init; }

    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, TotalCount);

    public int PreviousPage => Math.Max(1, Page - 1);

    public int NextPage => Math.Min(TotalPages, Page + 1);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedList<T> Create(IEnumerable<T> source, int page, int pageSize)
    {
        var safePageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 5, 100);
        var items = source.ToArray();
        var totalCount = items.Length;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)safePageSize);
        var safePage = Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);

        return new PagedList<T>
        {
            Items = items
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToArray(),
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
        };
    }
}
