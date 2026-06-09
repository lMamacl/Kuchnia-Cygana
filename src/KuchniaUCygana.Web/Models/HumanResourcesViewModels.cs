using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Web.Models;

public sealed class HumanResourcesDashboardViewModel
{
    public HumanResourcesSummaryDto Summary { get; init; } = new();

    public IReadOnlyList<DepartmentStaffSummaryDto> DepartmentSummaries { get; init; } = [];

    public IReadOnlyList<string> AvailableRoles { get; init; } = [];

    public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];

    public IReadOnlyList<EmployeeDto> Employees { get; init; } = [];

    public IReadOnlyList<LeaveRequestDto> LeaveRequests { get; init; } = [];

    public IReadOnlyList<WorkScheduleDto> WorkSchedules { get; init; } = [];

    public IReadOnlyList<UserDto> Users { get; init; } = [];

    public PagedList<DepartmentDto> DepartmentsPage { get; init; } = new();

    public PagedList<EmployeeDto> EmployeesPage { get; init; } = new();

    public PagedList<LeaveRequestDto> LeaveRequestsPage { get; init; } = new();

    public PagedList<WorkScheduleDto> WorkSchedulesPage { get; init; } = new();

    public DepartmentListFilterViewModel DepartmentsFilter { get; init; } = new();

    public EmployeeListFilterViewModel EmployeesFilter { get; init; } = new();

    public LeaveRequestListFilterViewModel LeaveRequestsFilter { get; init; } = new();

    public WorkScheduleListFilterViewModel WorkSchedulesFilter { get; init; } = new();

    public CreateDepartmentRequest NewDepartment { get; init; } = new();

    public CreateEmployeeRequest NewEmployee { get; init; } = new()
    {
        HireDate = DateOnly.FromDateTime(DateTime.Today),
        IsActive = true,
    };

    public CreateLeaveRequestRequest NewLeaveRequest { get; init; } = new()
    {
        StartDate = DateOnly.FromDateTime(DateTime.Today),
        EndDate = DateOnly.FromDateTime(DateTime.Today),
    };

    public CreateWorkScheduleRequest NewWorkSchedule { get; init; } = new()
    {
        ShiftDate = DateOnly.FromDateTime(DateTime.Today),
    };
}

public sealed class DepartmentListFilterViewModel : StaffListFilterViewModel
{
}

public sealed class EmployeeListFilterViewModel : StaffListFilterViewModel
{
    public int? DepartmentId { get; set; }

    public string? Status { get; set; }

    public string? Role { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        DepartmentId is > 0 ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Role);

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        AddIfSet(values, nameof(DepartmentId), DepartmentId);
        AddIfSet(values, nameof(Status), Status);
        AddIfSet(values, nameof(Role), Role);
        return values;
    }
}

public sealed class LeaveRequestListFilterViewModel : StaffListFilterViewModel
{
    public int? Status { get; set; }

    public int? LeaveType { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        Status.HasValue ||
        LeaveType.HasValue;

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        AddIfSet(values, nameof(Status), Status);
        AddIfSet(values, nameof(LeaveType), LeaveType);
        return values;
    }
}

public sealed class WorkScheduleListFilterViewModel : StaffListFilterViewModel
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? Shift { get; set; }

    public string? Role { get; set; }

    public override bool HasActiveCriteria =>
        base.HasActiveCriteria ||
        From.HasValue ||
        To.HasValue ||
        !string.IsNullOrWhiteSpace(Shift) ||
        !string.IsNullOrWhiteSpace(Role);

    public override IDictionary<string, object?> ToRouteValues()
    {
        var values = base.ToRouteValues();
        if (From.HasValue)
        {
            values[nameof(From)] = From.Value.ToString("yyyy-MM-dd");
        }

        if (To.HasValue)
        {
            values[nameof(To)] = To.Value.ToString("yyyy-MM-dd");
        }

        AddIfSet(values, nameof(Shift), Shift);
        AddIfSet(values, nameof(Role), Role);
        return values;
    }
}
