namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class StockTableFilterDto
{
    public string? Search { get; set; }

    public bool ShowExpiredOnly { get; set; }

    public bool ShowLowStockOnly { get; set; }
}
