namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class EmployeeDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateOnly HireDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public int DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public string Position { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? UserRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
