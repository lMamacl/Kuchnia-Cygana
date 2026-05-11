using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryWindowRepository : BaseRepository<DeliveryWindow>, IDeliveryWindowRepository
{
    public DeliveryWindowRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IEnumerable<DeliveryWindow>> GetActiveWindowsAsync() =>
        throw new NotImplementedException();
}
