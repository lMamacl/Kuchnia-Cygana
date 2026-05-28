using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

/// <summary>
/// Repozytorium logów zmian statusów sesji pakowania.
/// </summary>
public sealed class PackingStatusLogRepository : BaseRepository<PackingStatusLog>, IPackingStatusLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PackingStatusLogRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PackingStatusLog>> GetBySessionIdAsync(int packingSessionId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QueryAsync<PackingStatusLog>(
            """
            SELECT *
            FROM [PackingStatusLogs]
            WHERE [PackingSessionId] = @packingSessionId
            ORDER BY [ChangedAt] DESC;
            """,
            new { packingSessionId });
    }
}
