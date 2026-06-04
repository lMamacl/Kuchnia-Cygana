namespace KuchniaUCygana.Application.Interfaces;

public interface IPaymentService
{
    Task<(string PaymentIntentId, string ClientSecret)> CreatePaymentIntentAsync(decimal amount, string currency = "pln");
    Task<bool> VerifyPaymentIntentAsync(string paymentIntentId);
}
