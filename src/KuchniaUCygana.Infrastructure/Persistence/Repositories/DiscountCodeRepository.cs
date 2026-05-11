using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DiscountCodeRepository : BaseRepository<DiscountCode>, IDiscountCodeRepository
{
    public DiscountCodeRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<DiscountCode?> GetByCodeAsync(string code) =>
        throw new NotImplementedException();

    public Task IncrementUsageAsync(int discountCodeId) =>
        throw new NotImplementedException();
}
