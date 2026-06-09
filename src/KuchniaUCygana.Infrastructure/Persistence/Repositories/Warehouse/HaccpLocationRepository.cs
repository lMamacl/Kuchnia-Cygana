using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class HaccpLocationRepository : BaseRepository<HaccpLocation>, IHaccpLocationRepository
{
    public HaccpLocationRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<HaccpLocationDetailsRow>> GetAllWithCategoriesAsync(bool activeOnly)
    {
        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<HaccpLocationDetailsRow>(
            """
            SELECT
                hl.[Id],
                hl.[Code],
                hl.[Name],
                hl.[MinTemperatureCelsius],
                hl.[MaxTemperatureCelsius],
                hl.[IsActive],
                hl.[DisplayOrder],
                hl.[Notes],
                COALESCE(STRING_AGG(CONVERT(varchar(20), wc.[Id]), ','), '') AS [CategoryIds],
                COALESCE(STRING_AGG(wc.[Name], ', '), '') AS [CategoryNames]
            FROM [HaccpLocations] hl
            LEFT JOIN [HaccpLocationCategories] hlc ON hlc.[HaccpLocationId] = hl.[Id]
            LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = hlc.[WarehouseCategoryId]
            WHERE (@activeOnly = 0 OR hl.[IsActive] = 1)
            GROUP BY
                hl.[Id], hl.[Code], hl.[Name], hl.[MinTemperatureCelsius], hl.[MaxTemperatureCelsius],
                hl.[IsActive], hl.[DisplayOrder], hl.[Notes]
            ORDER BY hl.[DisplayOrder], hl.[Name], hl.[Id];
            """,
            new { activeOnly });
        return rows.ToList();
    }

    public async Task<HaccpLocationDetailsRow?> GetDetailsByIdAsync(int id)
    {
        var rows = await GetAllWithCategoriesAsync(activeOnly: false);
        return rows.FirstOrDefault(row => row.Id == id);
    }

    public async Task<IReadOnlyList<WarehouseCategory>> GetCategoriesForLocationAsync(int locationId)
    {
        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<WarehouseCategory>(
            """
            SELECT wc.*
            FROM [WarehouseCategories] wc
            INNER JOIN [HaccpLocationCategories] hlc ON hlc.[WarehouseCategoryId] = wc.[Id]
            WHERE hlc.[HaccpLocationId] = @locationId
            ORDER BY wc.[DisplayOrder], wc.[Name], wc.[Id];
            """,
            new { locationId });
        return rows.ToList();
    }

    public async Task ReplaceCategoriesAsync(int locationId, IReadOnlyCollection<int> categoryIds)
    {
        using var db = Factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        await db.ExecuteAsync(
            "DELETE FROM [HaccpLocationCategories] WHERE [HaccpLocationId] = @locationId;",
            new { locationId },
            tx);

        var distinctIds = categoryIds
            .Where(id => id > 0)
            .Distinct()
            .Select(id => new { HaccpLocationId = locationId, WarehouseCategoryId = id })
            .ToArray();

        if (distinctIds.Length > 0)
        {
            await db.ExecuteAsync(
                """
                INSERT INTO [HaccpLocationCategories] ([HaccpLocationId], [WarehouseCategoryId])
                VALUES (@HaccpLocationId, @WarehouseCategoryId);
                """,
                distinctIds,
                tx);
        }

        tx.Commit();
    }
}


