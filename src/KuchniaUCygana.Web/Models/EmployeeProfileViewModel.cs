using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Web.Models;

public sealed class EmployeeProfileViewModel
{
    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];

    public EmployeeDto? Employee { get; init; }

    public IReadOnlyList<WorkScheduleDto> WorkSchedules { get; init; } = [];

    public IReadOnlyList<LeaveRequestDto> LeaveRequests { get; init; } = [];

    public CreateLeaveRequestRequest NewLeaveRequest { get; init; } = new()
    {
        StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
        EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
    };
}
