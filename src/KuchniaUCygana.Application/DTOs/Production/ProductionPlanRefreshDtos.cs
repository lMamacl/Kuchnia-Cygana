namespace KuchniaUCygana.Application.DTOs.Production;

public sealed class ProductionPlanRefreshResultDto
{
    public DateOnly ProductionDate { get; set; }
    public int? ProductionPlanId { get; set; }
    public string Status { get; set; } = "Skipped";
    public int ItemCount { get; set; }
    public List<string> Blockers { get; set; } = new();
}

public sealed class ProductionPlanRefreshRangeResultDto
{
    public DateOnly StartDate { get; set; }
    public int Days { get; set; }
    public int CreatedCount { get; set; }
    public int RefreshedCount { get; set; }
    public int SkippedCount { get; set; }
    public int BlockedCount { get; set; }
    public List<ProductionPlanRefreshResultDto> Results { get; set; } = new();
}
