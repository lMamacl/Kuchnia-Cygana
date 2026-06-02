/*
 * Plik: Entities/HR/LeaveRequest.cs
 * Opis: Wniosek urlopowy pracownika – typ urlopu, zakres dat, status, osoba zatwierdzająca, ewentualny powód odrzucenia.
 */

using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.HR;


public sealed class LeaveRequest : AuditableEntity
{

    public int EmployeeId { get; set; }

    public int LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    
    public int Status { get; set; }


    public int? ApprovedByEmployeeId { get; set; }
    public string? RejectionReason { get; set; }
}