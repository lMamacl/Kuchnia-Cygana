using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Production;

/// <summary>
/// Partia produkcyjna półproduktów (buliony, sosy, ciasta).
/// Powiązanie: SubRecipe concept z class diagram.puml
/// </summary>
[Table("ProductionBatches")]
public class ProductionBatch : AuditableEntity<int>
{
    public int ProductionPlanId { get; set; }

    /// <summary>
    /// ID posiłku-półproduktu z Modułu 2 (bridge).
    /// </summary>
    public int MealId { get; set; }

    /// <summary>
    /// Nazwa półproduktu (denormalizowana).
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Planowana ilość (w gramach lub sztukach wg jednostki).
    /// </summary>
    public decimal PlannedQuantity { get; set; }

    /// <summary>
    /// Faktycznie wyprodukowana ilość.
    /// </summary>
    public decimal ProducedQuantity { get; set; }
}
