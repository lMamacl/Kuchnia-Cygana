namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class DepartmentDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? HeadEmployeeId { get; set; }

    public string? HeadEmployeeFullName { get; set; }
}
