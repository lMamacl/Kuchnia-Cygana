using System.Data;
using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium pojazdĂłw
/// </summary>
public sealed class VehicleRepository : BaseRepository<Vehicle>, IVehicleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VehicleRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IEnumerable<int> ids)
    {
        using var db = _connectionFactory.CreateConnection();
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<Vehicle>();
        }

        var vehicles = await db.QueryAsync<Vehicle>(
            "SELECT * FROM [Vehicles] WHERE [Id] IN @Ids AND [IsDeleted] = 0 ORDER BY [Id];",
            new { Ids = idList });
        return vehicles.ToList();
    }

    public async Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        using var db = _connectionFactory.CreateConnection();
        var vehicle = await db.QuerySingleOrDefaultAsync<Vehicle>(
            "SELECT * FROM [Vehicles] WHERE [RegistrationNumber] = @registrationNumber AND [IsDeleted] = 0;",
            new { registrationNumber });
        return vehicle;
    }
}


