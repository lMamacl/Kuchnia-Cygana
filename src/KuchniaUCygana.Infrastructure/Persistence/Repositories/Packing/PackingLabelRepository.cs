using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingLabelRepository : BaseRepository<PackingLabel>, IPackingLabelRepository
{
    public PackingLabelRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<PackingLabel?> GetLatestShippingForBagAsync(int packingBagId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingLabel>(
            """
            SELECT TOP 1 *
            FROM PackingLabels
            WHERE LabelType = @labelType
              AND PackingBagId = @packingBagId
            ORDER BY PrintNumber DESC, Id DESC;
            """,
            new
            {
                labelType = (int)LabelType.Shipping,
                packingBagId,
            });
    }

    public async Task<IReadOnlyDictionary<int, PackingLabel>> GetLatestShippingForBagsAsync(IEnumerable<int> packingBagIds)
    {
        var ids = packingBagIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, PackingLabel>();
        }

        using var db = Factory.CreateConnection();
        var labels = await db.QueryAsync<PackingLabel>(
            """
            WITH RankedLabels AS (
                SELECT *,
                       ROW_NUMBER() OVER (
                           PARTITION BY PackingBagId
                           ORDER BY PrintNumber DESC, Id DESC
                       ) AS RowNumber
                FROM PackingLabels
                WHERE LabelType = @labelType
                  AND PackingBagId IN @ids
            )
            SELECT *
            FROM RankedLabels
            WHERE RowNumber = 1;
            """,
            new
            {
                labelType = (int)LabelType.Shipping,
                ids,
            });

        return labels
            .Where(label => label.PackingBagId.HasValue)
            .ToDictionary(label => label.PackingBagId!.Value, label => label);
    }

    public async Task<IReadOnlyList<PackingLabel>> GetShippingForBagsAsync(IEnumerable<int> packingBagIds)
    {
        var ids = packingBagIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<PackingLabel>();
        }

        using var db = Factory.CreateConnection();
        var labels = await db.QueryAsync<PackingLabel>(
            """
            SELECT *
            FROM PackingLabels
            WHERE LabelType = @labelType
              AND PackingBagId IN @ids
            ORDER BY PackingBagId, PrintNumber DESC, Id DESC;
            """,
            new
            {
                labelType = (int)LabelType.Shipping,
                ids,
            });

        return labels.ToList();
    }

    public async Task<IReadOnlyList<PackingLabel>> GetShippingForSessionAsync(int packingSessionId)
    {
        using var db = Factory.CreateConnection();
        var labels = await db.QueryAsync<PackingLabel>(
            """
            SELECT *
            FROM PackingLabels
            WHERE LabelType = @labelType
              AND PackingSessionId = @packingSessionId
            ORDER BY PrintNumber DESC, Id DESC;
            """,
            new
            {
                labelType = (int)LabelType.Shipping,
                packingSessionId,
            });

        return labels.ToList();
    }

    public async Task<PackingLabel?> GetShippingByQrCodeAsync(string qrCode)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingLabel>(
            """
            SELECT TOP 1 *
            FROM PackingLabels
            WHERE LabelType = @labelType
              AND QrCode = @qrCode
            ORDER BY PrintNumber DESC, Id DESC;
            """,
            new
            {
                labelType = (int)LabelType.Shipping,
                qrCode,
            });
    }

    public async Task<int> GetShippingPrintCountAsync(int packingSessionId, int packingBagId)
    {
        using var db = Factory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM PackingLabels
            WHERE LabelType = @labelType
              AND (PackingBagId = @packingBagId OR PackingSessionId = @packingSessionId);
            """,
            new
            {
                labelType = (int)LabelType.Shipping,
                packingSessionId,
                packingBagId,
            });
    }
}
