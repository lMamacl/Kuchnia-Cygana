using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

[EnumAsInt]
public enum DeliveryStatus
{
    Scheduled = 0,
    Delivered = 1,
    Skipped = 2,
    Cancelled = 3,
}
