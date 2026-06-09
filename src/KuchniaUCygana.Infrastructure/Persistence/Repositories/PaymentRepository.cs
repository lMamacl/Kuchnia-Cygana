using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    public async Task<Payment?> GetByOrderIdAsync(int orderId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Payments WHERE OrderId = @OrderId AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<Payment>(sql, new { OrderId = orderId });
    }

    public async Task<Payment?> GetByStripeIntentIdAsync(string stripePaymentIntentId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Payments WHERE StripePaymentIntentId = @IntentId AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<Payment>(sql, new { IntentId = stripePaymentIntentId });
    }
}


