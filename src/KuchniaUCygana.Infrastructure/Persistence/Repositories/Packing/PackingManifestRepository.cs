using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingManifestRepository : BaseRepository<PackingManifest>, IPackingManifestRepository
{
    public PackingManifestRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<PackingManifest?> GetLatestAsync(DateOnly date, int routeId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingManifest>(
            """
            SELECT TOP 1 *
            FROM PackingManifests
            WHERE PackingDate = @date
              AND RouteId = @routeId
              AND IsSuperseded = 0
            ORDER BY GeneratedAt DESC, Id DESC;
            """,
            new
            {
                date = date.ToDateTime(TimeOnly.MinValue),
                routeId,
            });
    }

    public async Task<IReadOnlyDictionary<int, PackingManifest>> GetLatestByRoutesAsync(DateOnly date, IEnumerable<int> routeIds)
    {
        var ids = routeIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, PackingManifest>();
        }

        using var db = Factory.CreateConnection();
        var manifests = await db.QueryAsync<PackingManifest>(
            """
            WITH RankedManifests AS (
                SELECT *,
                       ROW_NUMBER() OVER (
                           PARTITION BY RouteId
                           ORDER BY GeneratedAt DESC, Id DESC
                       ) AS RowNumber
                FROM PackingManifests
                WHERE PackingDate = @date
                  AND RouteId IN @ids
                  AND IsSuperseded = 0
            )
            SELECT *
            FROM RankedManifests
            WHERE RowNumber = 1;
            """,
            new
            {
                date = date.ToDateTime(TimeOnly.MinValue),
                ids,
            });

        return manifests
            .Where(manifest => manifest.RouteId.HasValue)
            .ToDictionary(manifest => manifest.RouteId!.Value, manifest => manifest);
    }
}
