using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.Admin;

namespace KuchniaUCygana.Web.Models;

public sealed class AdminDashboardViewModel
{
    public IReadOnlyList<SystemLogDto> SystemLogs { get; init; } = [];

    public IReadOnlyList<UserDto> Users { get; init; } = [];

    public IReadOnlyList<string> AvailableRoles { get; init; } = [];

    public UserDirectorySummaryDto UserSummary { get; init; } = new();

    public PagedList<UserDto> UsersPage { get; init; } = new();

    public AdminUserListFilterViewModel UsersFilter { get; init; } = new();

    public SystemLogPageDto AuditPage { get; init; } = new();

    public AuditLogFilterViewModel AuditFilter { get; init; } = new();

    public CreateAdminUserViewModel NewUser { get; init; } = new();
}

public sealed class CreateAdminUserViewModel
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateAdminUserViewModel
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

public sealed class ResetAdminUserPasswordViewModel
{
    public int Id { get; set; }

    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AuditLogFilterViewModel
{
    public string? Search { get; set; }

    public int? UserId { get; set; }

    public string? Action { get; set; }

    public string? TargetEntity { get; set; }

    public DateTime? From { get; set; } = DateTime.Today.AddDays(-30);

    public DateTime? To { get; set; }

    public bool IncludeArchived { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public SystemLogSearchRequest ToSearchRequest()
    {
        return new SystemLogSearchRequest
        {
            Search = Search,
            UserId = UserId,
            Action = Action,
            TargetEntity = TargetEntity,
            From = From,
            To = To,
            IncludeArchived = IncludeArchived,
            Page = Page,
            PageSize = PageSize,
        };
    }

    public IDictionary<string, object?> ToRouteValues()
    {
        var values = new Dictionary<string, object?>();
        AddIfSet(values, nameof(Search), Search);
        AddIfSet(values, nameof(UserId), UserId);
        AddIfSet(values, nameof(Action), Action);
        AddIfSet(values, nameof(TargetEntity), TargetEntity);
        if (From.HasValue)
        {
            values[nameof(From)] = From.Value.ToString("yyyy-MM-dd");
        }

        if (To.HasValue)
        {
            values[nameof(To)] = To.Value.ToString("yyyy-MM-dd");
        }

        if (IncludeArchived)
        {
            values[nameof(IncludeArchived)] = true;
        }

        return values;
    }

    private static void AddIfSet(IDictionary<string, object?> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[key] = value;
        }
    }

    private static void AddIfSet<T>(IDictionary<string, object?> values, string key, T? value)
        where T : struct
    {
        if (value.HasValue)
        {
            values[key] = value.Value;
        }
    }
}

public sealed class AdminUserListFilterViewModel : StaffListFilterViewModel
{
    public string? Role { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        !string.IsNullOrWhiteSpace(Role);

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        AddIfSet(values, nameof(Role), Role);
        return values;
    }
}
