using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class Ingredient : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
