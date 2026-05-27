using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DiscountCodeRepository : BaseRepository<DiscountCode>, IDiscountCodeRepository
{
    public DiscountCodeRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<DiscountCode?> GetByCodeAsync(string code)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM DiscountCodes WHERE Code = @Code AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<DiscountCode>(sql, new { Code = code });
    }

    public async Task IncrementUsageAsync(int discountCodeId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "UPDATE DiscountCodes SET UsedCount = UsedCount + 1, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        await db.ExecuteAsync(sql, new { Id = discountCodeId, UpdatedAt = DateTimeOffset.UtcNow });
    }
}
