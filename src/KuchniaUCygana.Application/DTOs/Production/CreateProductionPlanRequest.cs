namespace KuchniaUCygana.Application.DTOs.Production;

/// <summary>
/// Request generowania planu produkcji na dany dzień.
/// </summary>
public sealed class CreateProductionPlanRequest
{
    /// <summary>
    /// Data produkcji (na kiedy generujemy plan).
    /// </summary>
    public DateOnly ProductionDate { get; set; }

    /// <summary>
    /// Uwagi Szefa Kuchni (opcjonalne).
    /// </summary>
    public string? Notes { get; set; }
}
