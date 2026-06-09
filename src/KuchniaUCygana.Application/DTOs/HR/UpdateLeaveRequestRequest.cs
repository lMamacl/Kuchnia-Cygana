namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class UpdateLeaveRequestRequest
{
    public int Id { get; set; }

    public int LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }
}
