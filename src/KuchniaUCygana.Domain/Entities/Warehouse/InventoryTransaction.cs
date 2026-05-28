using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("InventoryTransactions")]
public class InventoryTransaction : BaseEntity<long>
{
    public int BatchId { get; set; }

    public InventoryTransactionType TransactionType { get; set; }

    public decimal QuantityChanged { get; set; }

    [StringLength(250)]
    public string? Reason { get; set; }

    // E.g. "Order #123", "User #5 Adjustment"
    [StringLength(50)]
    public string? ReferenceDocument { get; set; }
}
