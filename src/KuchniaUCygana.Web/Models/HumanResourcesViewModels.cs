using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Web.Models;

public sealed class HumanResourcesDashboardViewModel
{
    public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];

    public IReadOnlyList<EmployeeDto> Employees { get; init; } = [];

    public IReadOnlyList<LeaveRequestDto> LeaveRequests { get; init; } = [];

    public IReadOnlyList<WorkScheduleDto> WorkSchedules { get; init; } = [];

    public IReadOnlyList<UserDto> Users { get; init; } = [];

    public PagedList<DepartmentDto> DepartmentsPage { get; init; } = new();

    public PagedList<EmployeeDto> EmployeesPage { get; init; } = new();

    public PagedList<LeaveRequestDto> LeaveRequestsPage { get; init; } = new();

    public PagedList<WorkScheduleDto> WorkSchedulesPage { get; init; } = new();

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
