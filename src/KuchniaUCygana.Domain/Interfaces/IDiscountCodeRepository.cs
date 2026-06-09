using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IDiscountCodeRepository : IRepository<DiscountCode>
{
    Task<DiscountCode?> GetByCodeAsync(string code);
    Task IncrementUsageAsync(int discountCodeId);
}
