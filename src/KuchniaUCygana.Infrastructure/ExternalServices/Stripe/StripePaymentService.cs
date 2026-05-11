using KuchniaUCygana.Application.Interfaces;

namespace KuchniaUCygana.Infrastructure.ExternalServices.Stripe;

public sealed class StripePaymentService : IPaymentService
{
    public Task<string> CreatePaymentIntentAsync(decimal amount, string currency)
    {
        return Task.FromResult($"mock_intent_{amount}_{currency}");
    }
}
