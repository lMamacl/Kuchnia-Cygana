using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingBagRepository : BaseRepository<PackingBag>, IPackingBagRepository
{
    public PackingBagRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<PackingBag>> GetBySessionIdAsync(int packingSessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<PackingBag>(
            """
            SELECT *
            FROM PackingBags
            WHERE PackingSessionId = @packingSessionId
              AND IsDeleted = 0
            ORDER BY BagNumber, Id;
            """,
            new { packingSessionId });
    }

    public async Task<IReadOnlyList<PackingBag>> GetBySessionIdsAsync(IEnumerable<int> packingSessionIds)
    {
        var ids = packingSessionIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<PackingBag>();
        }

        using var db = Factory.CreateConnection();
        var bags = await db.QueryAsync<PackingBag>(
            """
            SELECT *
            FROM PackingBags
            WHERE PackingSessionId IN @ids
              AND IsDeleted = 0
            ORDER BY PackingSessionId, BagNumber, Id;
            """,
            new { ids });

        return bags.ToList();
    }

    public async Task<PackingBag?> GetByCodeAsync(string bagCode)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingBag>(
            """
            SELECT TOP 1 *
            FROM PackingBags
            WHERE BagCode = @bagCode
              AND IsDeleted = 0
            ORDER BY Id DESC;
            """,
            new { bagCode });
    }

    public async Task<PackingBag?> GetDefaultForSessionAsync(int packingSessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingBag>(
            """
            SELECT TOP 1 *
            FROM PackingBags
            WHERE PackingSessionId = @packingSessionId
              AND IsDeleted = 0
              AND Status <> @damagedStatus
            ORDER BY BagNumber, Id;
            """,
            new { packingSessionId, damagedStatus = (int)PackingBagStatus.Damaged });
    }
}
