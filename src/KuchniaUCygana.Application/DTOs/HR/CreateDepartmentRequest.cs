namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class CreateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? HeadEmployeeId { get; set; }
}
