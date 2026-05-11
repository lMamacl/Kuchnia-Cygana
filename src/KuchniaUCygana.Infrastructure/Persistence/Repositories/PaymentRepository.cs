using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<Payment?> GetByOrderIdAsync(int orderId) =>
        throw new NotImplementedException();

    public Task<Payment?> GetByStripeIntentIdAsync(string stripePaymentIntentId) =>
        throw new NotImplementedException();
}
