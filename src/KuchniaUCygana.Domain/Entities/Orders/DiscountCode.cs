using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Table("DiscountCodes")]
public sealed class DiscountCode : AuditableEntity
{
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
