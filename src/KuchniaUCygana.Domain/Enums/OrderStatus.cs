using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

[EnumAsInt]
public enum OrderStatus
{
    Draft = 0,
    PendingPayment = 1,
    Paid = 2,
    InProduction = 3,
    Cancelled = 4,
}
