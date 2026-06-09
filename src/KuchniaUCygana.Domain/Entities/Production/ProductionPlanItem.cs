using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Production;

/// <summary>
/// Pozycja planu produkcji — ile porcji danego posiłku w danym wariancie ugotować.
/// Powiązanie: PozycjaPlanu z class diagram.puml
/// </summary>
[Table("ProductionPlanItems")]
public class ProductionPlanItem : AuditableEntity<int>
{
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

    public int? DietMenuPlanItemId { get; set; }

    [StringLength(500)]
    public string? RecipeComponentVersionIds { get; set; }

    [StringLength(64)]
    public string? M2SnapshotHash { get; set; }

    public string? M2SnapshotJson { get; set; }

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

    // === Model B-lite: przygotowanie pod etapowy rozwóz ===

    /// <summary>
    /// Grupa produkcyjna (1=zimne/śniadania, 2=zupy, 3=dania główne, 4=sałatki).
    /// Determinuje kolejność gotowania i umożliwia etapowy wyjazd aut.
    /// </summary>
    public int? ProductionGroup { get; set; }

    /// <summary>
    /// Szacowany czas gotowości (np. 07:00) — wyliczany z typu dania i ilości.
    /// Przekazywany do M4 do planowania tras.
    /// </summary>
    public TimeOnly? EstimatedReadyTime { get; set; }

    /// <summary>
    /// Rzeczywisty czas gotowości — ustawiany po zatwierdzeniu ugotowania.
    /// </summary>
    public TimeOnly? ActualReadyTime { get; set; }

    public DateTimeOffset? FefoDeductedAt { get; set; }

    [StringLength(50)]
    public string? FefoReferenceDocument { get; set; }

    public DateTimeOffset? PackagingDeductedAt { get; set; }

    [StringLength(50)]
    public string? PackagingReferenceDocument { get; set; }
}
