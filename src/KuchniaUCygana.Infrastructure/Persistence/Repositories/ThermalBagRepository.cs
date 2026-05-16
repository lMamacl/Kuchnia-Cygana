using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium toreb termicznych
/// </summary>
public sealed class ThermalBagRepository : BaseRepository<ThermalBag>, IThermalBagRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ThermalBagRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ThermalBag?> GetBySerialNumberAsync(string serialNumber)
    {
        using var db = _connectionFactory.CreateConnection();
        // W ServiceStack.OrmLite używamy SingleAsync do pobrania jednego elementu po warunku
        return await db.SingleAsync<ThermalBag>(t => t.SerialNumber == serialNumber);
    }
}