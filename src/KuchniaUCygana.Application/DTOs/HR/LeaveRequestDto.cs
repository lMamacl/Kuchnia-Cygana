namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class LeaveRequestDto
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
