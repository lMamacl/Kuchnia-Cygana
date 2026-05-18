namespace KuchniaUCygana.Application.DTOs.Production;

/// <summary>
/// DTO planu produkcji z listą pozycji — odpowiada ProductionPlan + Items.
/// </summary>
public sealed class ProductionPlanDto
{
    public int Id { get; set; }

    public DateOnly ProductionDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public bool IsSharedWithLogistics { get; set; }

    public DateTimeOffset? SharedAt { get; set; }

    public List<ProductionPlanItemDto> Items { get; set; } = new();

    /// <summary>
    /// Raport zapotrzebowania materiałowego (opcjonalny, generowany przy tworzeniu planu).
    /// </summary>
    public FoodCostReportDto? FoodCostReport { get; set; }
}
