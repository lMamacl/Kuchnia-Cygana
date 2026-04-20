using System;
using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Alias("Batches")]
public class Batch : AuditableEntity<int>
{
    [References(typeof(StockItem))]
    public int StockItemId { get; set; }

    [Required]
    [StringLength(50)]
    public string SupplierBatchNumber { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }
    public DateTimeOffset ReceivedDate { get; set; }

    public bool IsDepleted { get; set; }
}
