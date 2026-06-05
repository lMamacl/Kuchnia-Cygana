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
}


