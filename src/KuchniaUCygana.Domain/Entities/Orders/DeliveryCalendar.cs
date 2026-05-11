using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Orders;

// Jeden rekord = jeden dzień dostawy w ramach zamówienia.
// M3 pobiera rekordy na dany dzień, żeby wygenerować plan produkcji.

[Alias("DeliveryCalendar")]
public sealed class DeliveryCalendar : AuditableEntity
{
    public int OrderId { get; set; }

    public int AddressId { get; set; }

    public int? DeliveryWindowId { get; set; }

    public DateTime DeliveryDate { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Scheduled;

    public bool IsSkipped { get; set; }

    public string? SkipReason { get; set; }

    public DateTimeOffset? CutoffTime { get; set; }
}
