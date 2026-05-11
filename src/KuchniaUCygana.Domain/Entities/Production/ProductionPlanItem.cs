using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Production;

/// <summary>
/// Pozycja planu produkcji — ile porcji danego posiłku w danym wariancie ugotować.
/// Powiązanie: PozycjaPlanu z class diagram.puml
/// </summary>
[Alias("ProductionPlanItems")]
public class ProductionPlanItem : AuditableEntity<int>
{
    [References(typeof(ProductionPlan))]
    public int ProductionPlanId { get; set; }

    /// <summary>
    /// ID posiłku z Modułu 2 (bridge — bez strict FK).
    /// </summary>
    public int MealId { get; set; }

    /// <summary>
    /// Nazwa posiłku (denormalizowana dla wydruków i offline).
    /// </summary>
    [StringLength(200)]
    public string MealName { get; set; } = string.Empty;

    /// <summary>
    /// ID wariantu kalorycznego z Modułu 2 (bridge).
    /// </summary>
    public int DietVariantId { get; set; }

    /// <summary>
    /// Zaplanowana ilość porcji.
    /// </summary>
    public int PlannedQuantity { get; set; }

    /// <summary>
    /// Ugotowana ilość porcji — aktualizowana przez Szefa Kuchni.
    /// </summary>
    public int CookedQuantity { get; set; }

    /// <summary>
    /// Status realizacji pozycji.
    /// </summary>
    public ProductionItemStatus Status { get; set; } = ProductionItemStatus.Planned;
}
