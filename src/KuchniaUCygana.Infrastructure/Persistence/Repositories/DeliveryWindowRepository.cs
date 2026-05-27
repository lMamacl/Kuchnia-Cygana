using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryWindowRepository : BaseRepository<DeliveryWindow>, IDeliveryWindowRepository
{
    public DeliveryWindowRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<DeliveryWindow>> GetActiveWindowsAsync()
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM DeliveryWindows WHERE IsActive = 1 ORDER BY SortOrder";
        return await db.QueryAsync<DeliveryWindow>(sql);
    }
}
