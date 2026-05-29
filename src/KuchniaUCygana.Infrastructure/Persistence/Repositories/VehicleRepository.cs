using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium pojazdów
/// </summary>
public sealed class VehicleRepository : BaseRepository<Vehicle>, IVehicleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VehicleRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
