namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class WorkScheduleDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? EmployeeFullName { get; set; }

    public string? UserRole { get; set; }

    public DateOnly ShiftDate { get; set; }

    public string Shift { get; set; } = string.Empty;

    public string? RoleAtShift { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
