using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;

namespace KuchniaUCygana.Application.Services;

public sealed class DiscountService : IDiscountService
{
    private readonly IDiscountCodeRepository discountCodeRepository;
    private readonly IOrderRepository orderRepository;

    public DiscountService(
        IDiscountCodeRepository discountCodeRepository,
        IOrderRepository orderRepository)
    {
        this.discountCodeRepository = discountCodeRepository;
        this.orderRepository = orderRepository;
    }

    public Task<DiscountValidationResult> ValidateAndApplyAsync(
        string code, int orderId, int customerId) =>
        throw new NotImplementedException();

    public Task<bool> RemoveDiscountAsync(int orderId, int customerId) =>
        throw new NotImplementedException();
}
