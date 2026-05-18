namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// DTO partii magazynowej — odpowiada Batch z Domain.
/// </summary>
public sealed class BatchDto
{
    public int Id { get; set; }

    public int StockItemId { get; set; }

    public string SupplierBatchNumber { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset ReceivedDate { get; set; }

    public bool IsDepleted { get; set; }
}
