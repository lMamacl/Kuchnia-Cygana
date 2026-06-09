using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IHumanResourcesReadRepository
{
    Task<HumanResourcesSummaryRow> GetSummaryAsync(DateOnly today);

    Task<IReadOnlyList<DepartmentStaffSummaryRow>> GetDepartmentStaffSummariesAsync(string? search = null);

    Task<DepartmentSearchResult> SearchDepartmentsAsync(DepartmentSearchQuery query);

    Task<EmployeeSearchResult> SearchEmployeesAsync(EmployeeSearchQuery query);

    Task<EmployeeSearchRow?> GetEmployeeByUserIdAsync(int userId);

    Task<IReadOnlyList<EmployeeOptionRow>> GetEmployeeOptionsAsync(bool activeOnly, int limit);

    Task<IReadOnlyList<EmployeeOptionRow>> GetEmployeeOptionsByIdsAsync(IEnumerable<int> employeeIds);

    Task<IReadOnlyList<EmployeeOptionRow>> GetEmployeeOptionsByUserIdsAsync(IEnumerable<int> userIds);

    Task<LeaveRequestSearchResult> SearchLeaveRequestsAsync(LeaveRequestSearchQuery query);

    Task<IReadOnlyList<LeaveRequestSearchRow>> GetLeaveRequestsByEmployeeAsync(int employeeId);

    Task<WorkScheduleSearchResult> SearchWorkSchedulesAsync(WorkScheduleSearchQuery query);
}

public sealed record DepartmentSearchQuery(string? Search, int Page, int PageSize);

public sealed record EmployeeSearchQuery(
    string? Search,
    int? DepartmentId,
    string? Status,
    string? Role,
    int Page,
    int PageSize);

public sealed record LeaveRequestSearchQuery(
    int? Status,
    int? LeaveType,
    string? Search,
    int Page,
    int PageSize);

public sealed record WorkScheduleSearchQuery(
    DateOnly? From,
    DateOnly? To,
    WorkShift? Shift,
    string? Role,
    string? Search,
    int Page,
    int PageSize);

public sealed class DepartmentSearchResult : PagedSearchResult<DepartmentSearchRow>
{
}

public sealed class EmployeeSearchResult : PagedSearchResult<EmployeeSearchRow>
{
}

public sealed class LeaveRequestSearchResult : PagedSearchResult<LeaveRequestSearchRow>
{
}

public sealed class WorkScheduleSearchResult : PagedSearchResult<WorkScheduleSearchRow>
{
}

public abstract class PagedSearchResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class HumanResourcesSummaryRow
{
    public int PendingLeaveCount { get; set; }

    public int ActiveEmployeeCount { get; set; }

    public int InactiveEmployeeCount { get; set; }

    public int TodayScheduleCount { get; set; }
}

public sealed class DepartmentStaffSummaryRow
{
    public int DepartmentId { get; set; }

    public string DepartmentName { get; set; } = string.Empty;

    public int ActiveEmployeeCount { get; set; }

    public int TotalEmployeeCount { get; set; }

    public string? HeadEmployeeFullName { get; set; }

    public string SampleEmployeeNames { get; set; } = string.Empty;
}

public sealed class DepartmentSearchRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? HeadEmployeeId { get; set; }

    public string? HeadEmployeeFullName { get; set; }

    public int EmployeeCount { get; set; }

    public string SampleEmployeeNames { get; set; } = string.Empty;
}

public sealed class EmployeeSearchRow
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateOnly HireDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public int DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public string Position { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? UserRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class EmployeeOptionRow
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public sealed class LeaveRequestSearchRow
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public string? EmployeeFullName { get; set; }

    public int LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int Status { get; set; }

    public int? ApprovedByEmployeeId { get; set; }

    public string? ApprovedByEmployeeFullName { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class WorkScheduleSearchRow
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? EmployeeFullName { get; set; }

    public string? UserRole { get; set; }

    public DateOnly ShiftDate { get; set; }

    public WorkShift Shift { get; set; }

    public string? RoleAtShift { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
