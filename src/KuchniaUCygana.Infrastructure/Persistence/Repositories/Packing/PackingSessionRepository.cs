using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingSessionRepository : BaseRepository<PackingSession>, IPackingSessionRepository
{
    public PackingSessionRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<PackingSession>> GetActiveByDateAsync(DateOnly date)
    {
        using var db = Factory.CreateConnection();
        return await db.SqlListAsync<PackingSession>(
            """
            SELECT *
            FROM PackingSessions
            WHERE PackingDate = @date
              AND IsDeleted = 0
              AND Status <> @dispatchedStatus
            ORDER BY Id;
            """,
            new
            {
                date = date.ToDateTime(TimeOnly.MinValue),
                dispatchedStatus = (int)PackingStatus.Dispatched,
            });
    }

    public async Task<PackingSession?> GetWithItemsAsync(int sessionId)
    {
        using var db = Factory.CreateConnection();
        var session = await db.SingleAsync<PackingSession>(
            """
            SELECT *
            FROM PackingSessions
            WHERE Id = @sessionId
              AND IsDeleted = 0;
            """,
            new { sessionId });

        if (session is null)
        {
            return null;
        }

        session.Items = await db.SqlListAsync<PackingItem>(
            """
            SELECT *
            FROM PackingItems
            WHERE PackingSessionId = @sessionId
              AND IsDeleted = 0
            ORDER BY Id;
            """,
            new { sessionId });

        return session;
    }

    public async Task<IEnumerable<PackingItem>> GetSessionItemsAsync(int sessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.SqlListAsync<PackingItem>(
            """
            SELECT *
            FROM PackingItems
            WHERE PackingSessionId = @sessionId
              AND IsDeleted = 0
            ORDER BY Id;
            """,
            new { sessionId });
    }
}
