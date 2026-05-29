using KuchniaUCygana.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace KuchniaUCygana.Infrastructure.ExternalServices.Stripe;

public sealed class StripePaymentService : IPaymentService
{
    private readonly string secretKey;

    public StripePaymentService(IConfiguration configuration)
    {
        secretKey = configuration["Stripe:SecretKey"]
            ?? Environment.GetEnvironmentVariable("STRIPE_SECRET")
            ?? string.Empty;
    }

    public async Task<(string PaymentIntentId, string ClientSecret)> CreatePaymentIntentAsync(decimal amount, string currency = "pln")
    {
        if (amount <= 0m)
        {
            throw new InvalidOperationException("Kwota platnosci musi byc wieksza od zera.");
        }

        var options = new PaymentIntentCreateOptions
        {
            // Stripe wymaga kwoty w najmniejszej jednostce waluty, czyli w PLN w groszach.
            Amount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
            Currency = currency.ToLowerInvariant(),
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
        };

        var service = new PaymentIntentService();
        PaymentIntent intent = await service.CreateAsync(options, CreateRequestOptions());

        return (intent.Id, intent.ClientSecret);
    }

    public async Task<bool> VerifyPaymentIntentAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        PaymentIntent intent = await service.GetAsync(paymentIntentId, requestOptions: CreateRequestOptions());

        return intent.Status == "succeeded";
    }

    private RequestOptions CreateRequestOptions()
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Brakuje konfiguracji Stripe:SecretKey.");
        }

        return new RequestOptions
        {
            ApiKey = secretKey,
        };
    }
}
