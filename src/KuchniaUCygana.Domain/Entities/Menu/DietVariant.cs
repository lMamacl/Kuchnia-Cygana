using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class DietVariant : AuditableEntity
{
    public int DietId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public decimal PriceMultiplier { get; set; } = 1.0m;

    public bool IsDefault { get; set; } = false;
}
