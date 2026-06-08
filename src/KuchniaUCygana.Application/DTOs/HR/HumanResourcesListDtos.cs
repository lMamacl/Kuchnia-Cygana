using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class HumanResourcesSummaryDto
{
    public int PendingLeaveCount { get; init; }

    public int ActiveEmployeeCount { get; init; }

    public int InactiveEmployeeCount { get; init; }

    public int TodayScheduleCount { get; init; }
}

public sealed class DepartmentStaffSummaryDto
{
    public int DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public int ActiveEmployeeCount { get; init; }

    public int TotalEmployeeCount { get; init; }

    public string? HeadEmployeeFullName { get; init; }

    public string SampleEmployeeNames { get; init; } = string.Empty;
}

public sealed class DepartmentPageDto : PageDto<DepartmentDto>
{
}

public sealed class EmployeePageDto : PageDto<EmployeeDto>
{
}

public sealed class LeaveRequestPageDto : PageDto<LeaveRequestDto>
{
}

public sealed class WorkSchedulePageDto : PageDto<WorkScheduleDto>
{
}

public abstract class PageDto<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class DepartmentSearchRequest
{
    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public sealed class EmployeeSearchRequest
{
    public string? Search { get; init; }

    public int? DepartmentId { get; init; }

    public string? Status { get; init; }

    public string? Role { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public sealed class LeaveRequestSearchRequest
{
    public int? Status { get; init; }

    public int? LeaveType { get; init; }

    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public sealed class WorkScheduleSearchRequest
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public WorkShift? Shift { get; init; }

    public string? Role { get; init; }

    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
