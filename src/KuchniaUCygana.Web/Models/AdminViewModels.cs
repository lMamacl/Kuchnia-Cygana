using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.Admin;

namespace KuchniaUCygana.Web.Models;

public sealed class AdminDashboardViewModel
{
    public IReadOnlyList<SystemLogDto> SystemLogs { get; init; } = [];

    public IReadOnlyList<UserDto> Users { get; init; } = [];

    public IReadOnlyList<string> AvailableRoles { get; init; } = [];

    public SystemLogPageDto AuditPage { get; init; } = new();

    public AuditLogFilterViewModel AuditFilter { get; init; } = new();
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

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

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
            Page = Page,
            PageSize = PageSize,
        };
    }
}
