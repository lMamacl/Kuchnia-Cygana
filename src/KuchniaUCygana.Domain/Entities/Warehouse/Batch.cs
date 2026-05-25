using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("Batches")]
public class Batch : AuditableEntity<int>
{
    public int StockItemId { get; set; }

    [Required]
    [StringLength(50)]
    public string SupplierBatchNumber { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }
    public DateTimeOffset ReceivedDate { get; set; }

    public bool IsDepleted { get; set; }
}
