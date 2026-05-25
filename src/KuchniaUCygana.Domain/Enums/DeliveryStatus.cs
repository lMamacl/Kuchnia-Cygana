using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Enums;
public enum DeliveryStatus
{
    Scheduled = 0,
    Delivered = 1,
    Skipped = 2,
    Cancelled = 3,
}
