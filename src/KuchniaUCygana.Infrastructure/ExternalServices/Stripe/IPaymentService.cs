namespace KuchniaUCygana.Infrastructure.ExternalServices.Stripe;

public interface IPaymentService
{
    Task<string> CreatePaymentIntentAsync(decimal amount, string currency);
}
