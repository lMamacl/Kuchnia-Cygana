using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Orders;

[Alias("Payments")]
public sealed class Payment : AuditableEntity
{
    public int OrderId { get; set; }

    public string StripePaymentIntentId { get; set; } = string.Empty;

    public string? StripeClientSecret { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "PLN";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
}
