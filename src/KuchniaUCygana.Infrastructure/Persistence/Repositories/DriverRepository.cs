using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium kierowcĂłw
/// </summary>
public sealed class DriverRepository : BaseRepository<Driver>, IDriverRepository
{
    public DriverRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<Driver>> GetByIdsAsync(IEnumerable<int> ids)
    {
        using var db = Factory.CreateConnection();
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<Driver>();
        }

        var drivers = await db.QueryAsync<Driver>(
            "SELECT * FROM [Drivers] WHERE [Id] IN @Ids AND [IsDeleted] = 0 ORDER BY [Id];",
            new { Ids = idList });
        return drivers.ToList();
    }

    // Dodatkowe metody specyficzne dla kierowcĂłw, np.:
    public async Task<Driver?> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<Driver>(
            "SELECT * FROM [Drivers] WHERE [UserId] = @userId AND [IsDeleted] = 0;",
            new { userId });
    }

    public async Task<Driver?> GetByLicenseNumberAsync(string licenseNumber)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<Driver>(
            "SELECT * FROM [Drivers] WHERE [LicenseNumber] = @licenseNumber AND [IsDeleted] = 0;",
            new { licenseNumber });
    }

    public async Task<IReadOnlyList<Driver>> GetByIdsAsync(IReadOnlyCollection<int> driverIds)
    {
        var ids = driverIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Driver>();
        }

        using var db = Factory.CreateConnection();
        var drivers = await db.QueryAsync<Driver>(
            """
            SELECT *
            FROM [Drivers]
            WHERE [Id] IN @Ids
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { Ids = ids });

        return drivers.ToList();
    }
}


