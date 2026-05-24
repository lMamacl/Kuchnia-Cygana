using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Enums;

[EnumAsInt]
public enum DiscountType
{
    Percentage = 0,
    Fixed = 1,
}
