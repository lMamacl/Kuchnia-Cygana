using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Menu;

public sealed class Meal : AuditableEntity
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public MealStatus Status { get; set; } = MealStatus.Draft;

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}
