namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class WarehouseCategoryDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
