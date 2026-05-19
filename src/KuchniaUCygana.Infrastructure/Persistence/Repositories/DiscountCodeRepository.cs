using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DiscountCodeRepository : BaseRepository<DiscountCode>, IDiscountCodeRepository
{
    public DiscountCodeRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<DiscountCode?> GetByCodeAsync(string code)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleAsync<DiscountCode>(x =>
            x.Code == code && x.IsDeleted == false);
    }

    public async Task IncrementUsageAsync(int discountCodeId)
    {
        using var db = Factory.CreateConnection();
        var code = await db.SingleByIdAsync<DiscountCode>(discountCodeId);
        if (code is null) return;

        code.UsedCount++;
        code.UpdatedAt = DateTimeOffset.UtcNow;
        await db.UpdateAsync(code);
    }
}
