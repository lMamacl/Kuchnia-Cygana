using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class CreateWorkScheduleRequest
{
    public int UserId { get; set; }

    public DateOnly ShiftDate { get; set; }

    public WorkShift Shift { get; set; }

    public string? RoleAtShift { get; set; }
}
