using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryWindowRepository : BaseRepository<DeliveryWindow>, IDeliveryWindowRepository
{
    public DeliveryWindowRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<DeliveryWindow>> GetActiveWindowsAsync()
    {
        using var db = Factory.CreateConnection();
        var q = db.From<DeliveryWindow>()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.SortOrder);
        return await db.SelectAsync(q);
    }
}
