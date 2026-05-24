using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

[EnumAsInt]
public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Refunded = 4,
}
