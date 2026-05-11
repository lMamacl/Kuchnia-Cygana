using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

[EnumAsInt]
public enum InventoryTransactionType
{
    Receipt = 1,
    ProductionIssue = 2,
    Adjustment = 3,
    Waste = 4,
}
