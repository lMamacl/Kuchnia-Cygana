using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Enums;
public enum InventoryTransactionType
{
    Receipt = 1,
    ProductionIssue = 2,
    Adjustment = 3,
    Waste = 4,
    ManualIssue = 5,
    ExpiryDateChanged = 6,
}

