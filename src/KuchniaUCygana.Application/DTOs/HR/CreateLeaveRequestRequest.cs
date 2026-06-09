namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class CreateLeaveRequestRequest
{
    public int EmployeeId { get; set; }

    public int LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }
}
