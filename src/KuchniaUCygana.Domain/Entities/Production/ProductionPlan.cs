using System;
using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Production;

/// <summary>
/// Dzienny plan produkcji — harmonogram posiłków do ugotowania.
/// Generowany automatycznie (o 22:00) lub ręcznie przez Szefa Kuchni.
/// Powiązanie: PlanProdukcji z class diagram.puml
/// </summary>
[Alias("ProductionPlans")]
public class ProductionPlan : AuditableEntity<int>
{
    /// <summary>
    /// Data realizacji planu (na kiedy gotować).
    /// </summary>
    public DateOnly ProductionDate { get; set; }

    /// <summary>
    /// Status cyklu życia planu.
    /// </summary>
    public ProductionPlanStatus Status { get; set; } = ProductionPlanStatus.Draft;

    /// <summary>
    /// Uwagi Szefa Kuchni (np. "priorytet: zupa pomidorowa").
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
