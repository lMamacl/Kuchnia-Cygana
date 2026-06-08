using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryWindowRepository : BaseRepository<DeliveryWindow>, IDeliveryWindowRepository
{
    public DeliveryWindowRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    public async Task<IReadOnlyList<DeliveryWindow>> GetByIdsAsync(IEnumerable<int> ids)
    {
        using var db = Factory.CreateConnection();
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<DeliveryWindow>();
        }

        const string sql = """
            SELECT *
            FROM DeliveryWindows
            WHERE Id IN @Ids
            ORDER BY SortOrder, Id;
            """;

        var windows = await db.QueryAsync<DeliveryWindow>(sql, new { Ids = idList });
        return windows.ToList();
    }

    public async Task<IEnumerable<DeliveryWindow>> GetActiveWindowsAsync()
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM DeliveryWindows WHERE IsActive = 1 ORDER BY SortOrder";
        return await db.QueryAsync<DeliveryWindow>(sql);
    }
}


