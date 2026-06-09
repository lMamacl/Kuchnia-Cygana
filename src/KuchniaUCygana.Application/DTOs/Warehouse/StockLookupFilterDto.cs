namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class StockLookupFilterDto
{
    public string? Query { get; set; }

    public int Limit { get; set; } = 20;

    public bool OnlyAvailable { get; set; }
}
