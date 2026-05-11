using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface IDiscountService
{
    Task<DiscountValidationResult> ValidateAndApplyAsync(string code, int orderId, int customerId);
    Task<bool> RemoveDiscountAsync(int orderId, int customerId);
}
