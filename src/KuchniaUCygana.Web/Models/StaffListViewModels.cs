namespace KuchniaUCygana.Web.Models;

public class StaffListFilterViewModel
{
    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public virtual bool HasActiveCriteria => !string.IsNullOrWhiteSpace(Search);

    public virtual IDictionary<string, object?> ToRouteValues()
    {
        var values = new Dictionary<string, object?>();
        AddIfSet(values, nameof(Search), Search);
        return values;
    }

    protected static void AddIfSet(IDictionary<string, object?> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[key] = value;
        }
    }

    protected static void AddIfSet<T>(IDictionary<string, object?> values, string key, T? value)
        where T : struct
    {
        if (value.HasValue)
        {
            values[key] = value.Value;
        }
    }
}

public sealed class StaffPaginationViewModel
{
    public string Controller { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public int TotalCount { get; init; }

    public IReadOnlyDictionary<string, object?> RouteValues { get; init; } =
        new Dictionary<string, object?>();

    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, TotalCount);

    public int PreviousPage => Math.Max(1, Page - 1);

    public int NextPage => Math.Min(TotalPages, Page + 1);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static StaffPaginationViewModel FromPagedList<T>(
        PagedList<T> page,
        string controller,
        string action,
        IDictionary<string, object?>? routeValues = null)
        => new()
        {
            Controller = controller,
            Action = action,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            RouteValues = new Dictionary<string, object?>(routeValues ?? new Dictionary<string, object?>()),
        };

    public static StaffPaginationViewModel Create(
        string controller,
        string action,
        int page,
        int pageSize,
        int totalCount,
        IDictionary<string, object?>? routeValues = null)
        => new()
        {
            Controller = controller,
            Action = action,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 200),
            TotalCount = Math.Max(0, totalCount),
            RouteValues = new Dictionary<string, object?>(routeValues ?? new Dictionary<string, object?>()),
        };
}
