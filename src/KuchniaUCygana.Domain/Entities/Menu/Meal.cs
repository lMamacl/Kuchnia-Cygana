using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("Meals")]
public sealed class Meal : AuditableEntity
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string? PreparationInstructions { get; set; }

    public MealStatus Status { get; set; } = MealStatus.Draft;

    public int PreparationTimeMinutes { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsActive { get; set; } = true;
}
