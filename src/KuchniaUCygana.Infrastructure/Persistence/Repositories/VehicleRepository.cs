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
}