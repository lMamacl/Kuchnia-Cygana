namespace KuchniaUCygana.Application.DTOs.HR;

public sealed class UpdateDepartmentRequest
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? HeadEmployeeId { get; set; }
}
