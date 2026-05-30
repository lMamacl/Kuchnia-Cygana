namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class StockTableFilterDto
{
    public string? Search { get; set; }

    public string? Category { get; set; }

    public bool ShowExpiredOnly { get; set; }

    public bool ShowLowStockOnly { get; set; }

    public bool ShowExpiringSoonOnly { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 15;

    public int TotalCount { get; set; }
}
