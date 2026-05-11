using KuchniaUCygana.Domain.Common;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Alias("DeliveryWindows")]
public sealed class DeliveryWindow : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
