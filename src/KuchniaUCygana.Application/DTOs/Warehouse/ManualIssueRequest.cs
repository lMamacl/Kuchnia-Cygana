namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class ManualIssueRequest
{
    public int StockItemId { get; set; }

    public decimal Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string IssuedTo { get; set; } = string.Empty;
}
