using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface ICheckoutService
{
    Task<CheckoutSummaryDto> BuildSummaryAsync(int orderId, int customerId);
    Task<string> InitiatePaymentAsync(int orderId, int customerId);
    Task<bool> ConfirmPaymentAsync(string stripePaymentIntentId);
    Task<bool> HandlePaymentFailureAsync(string paymentIntentId, string errorMessage);
}
