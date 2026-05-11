using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class CheckoutService : ICheckoutService
{
    private readonly IOrderRepository orderRepository;
    private readonly IPaymentRepository paymentRepository;
    private readonly IPaymentService paymentService;

    public CheckoutService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IPaymentService paymentService)
    {
        this.orderRepository = orderRepository;
        this.paymentRepository = paymentRepository;
        this.paymentService = paymentService;
    }

    public Task<CheckoutSummaryDto> BuildSummaryAsync(int orderId, int customerId) =>
        throw new NotImplementedException();

    public Task<string> InitiatePaymentAsync(int orderId, int customerId) =>
        throw new NotImplementedException();

    public Task<bool> ConfirmPaymentAsync(string stripePaymentIntentId) =>
        throw new NotImplementedException();

    public Task<bool> HandlePaymentFailureAsync(string stripePaymentIntentId) =>
        throw new NotImplementedException();

    public Task<bool> CancelOrderOnMaxAttemptsAsync(int orderId) =>
        throw new NotImplementedException();
}
