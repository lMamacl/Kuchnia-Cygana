using System;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.HR;

public sealed class Employee : AuditableEntity
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public int DepartmentId { get; set; }
    public string Position { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
