using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Admin;

public sealed class WorkSchedule : AuditableEntity
{
    public int UserId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public WorkShift Shift { get; set; }
    public string? RoleAtShift { get; set; }
}