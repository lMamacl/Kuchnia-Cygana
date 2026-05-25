using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public class BatchRepository : BaseRepository<Batch>, IBatchRepository
{
    public BatchRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Batch>> GetActiveBatchesByStockItemAsync(int stockItemId)
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Batch>(
            """
            SELECT *
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDepleted] = 0
              AND [IsDeleted] = 0
            ORDER BY
              CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
              [ExpiryDate];
            """,
            new { stockItemId });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Batch>> GetExpiringBeforeAsync(DateTimeOffset date)
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Batch>(
            """
            SELECT *
            FROM [Batches]
            WHERE [ExpiryDate] IS NOT NULL
              AND [ExpiryDate] <= @date
              AND [IsDepleted] = 0
              AND [IsDeleted] = 0;
            """,
            new { date });
    }
}
