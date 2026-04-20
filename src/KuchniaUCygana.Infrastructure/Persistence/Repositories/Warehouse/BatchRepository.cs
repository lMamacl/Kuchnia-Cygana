using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public class BatchRepository : BaseRepository<Batch>, IBatchRepository
{
    public BatchRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Batch>> GetActiveBatchesByStockItemAsync(int stockItemId)
    {
        using var db = Factory.CreateConnection();

        var batches = await db.SelectAsync<Batch>(b =>
            b.StockItemId == stockItemId &&
            !b.IsDepleted &&
            !b.IsDeleted);

        // FEFO: partie z najwcześniejszą datą ważności idą pierwsze.
        // Partie bez daty ważności (null) trafiają na koniec kolejki.
        return batches
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Batch>> GetExpiringBeforeAsync(DateTimeOffset date)
    {
        using var db = Factory.CreateConnection();

        // SQLite przechowuje daty jako TEXT w formacie ISO8601;
        // OrmLite tłumaczy porównania dat poprawnie.
        var cutoff = date.UtcDateTime;

        return await db.SelectAsync<Batch>(b =>
            b.ExpiryDate != null &&
            b.ExpiryDate <= cutoff &&
            !b.IsDepleted &&
            !b.IsDeleted);
    }
}
