using KuchniaUCygana.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Menu;

[Table("Ingredients")]
public sealed class Ingredient : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal CostPerUnit { get; set; }

    public string? Notes { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool RequiresCoreTemperatureCheck { get; set; }

    public decimal? MinimumCoreTemperatureCelsius { get; set; }

    public bool IsActive { get; set; } = true;
}
