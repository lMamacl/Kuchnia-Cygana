using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Alias("DiscountCodes")]
public sealed class DiscountCode : AuditableEntity
{
    [Index(Unique = true)]
    public string Code { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? ValidFrom { get; set; }

    public DateTimeOffset? ValidTo { get; set; }

    public int? MaxUsageCount { get; set; }

    public int UsedCount { get; set; }

    public decimal? MinimumOrderValue { get; set; }
}
