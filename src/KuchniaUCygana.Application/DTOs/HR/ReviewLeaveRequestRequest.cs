namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class ReviewLeaveRequestRequest
{
    public int Id { get; set; }

    public int Status { get; set; }

    public int? ApprovedByEmployeeId { get; set; }

    public string? RejectionReason { get; set; }
}
