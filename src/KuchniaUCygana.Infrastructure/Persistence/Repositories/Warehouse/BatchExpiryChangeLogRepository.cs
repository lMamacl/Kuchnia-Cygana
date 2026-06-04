using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

/// <summary>
/// Repozytorium logów zmian dat ważności partii.
/// </summary>
public sealed class BatchExpiryChangeLogRepository : BaseRepository<BatchExpiryChangeLog>, IBatchExpiryChangeLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BatchExpiryChangeLogRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BatchExpiryChangeLog>> GetByBatchIdAsync(int batchId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QueryAsync<BatchExpiryChangeLog>(
            """
            SELECT *
            FROM [BatchExpiryChangeLogs]
            WHERE [BatchId] = @batchId
            ORDER BY [ChangedAt] DESC;
            """,
            new { batchId });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BatchExpiryChangeLog>> GetByStockItemIdAsync(int stockItemId)
    {
        using var db = _connectionFactory.CreateConnection();

        return await db.QueryAsync<BatchExpiryChangeLog>(
            """
            SELECT ecl.*
            FROM [BatchExpiryChangeLogs] ecl
            INNER JOIN [Batches] b ON b.[Id] = ecl.[BatchId]
            WHERE b.[StockItemId] = @stockItemId
            ORDER BY ecl.[ChangedAt] DESC;
            """,
            new { stockItemId });
    }
}
