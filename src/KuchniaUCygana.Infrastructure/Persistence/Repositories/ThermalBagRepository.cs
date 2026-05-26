using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using System.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Dapper;

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
        return await db.QueryFirstOrDefaultAsync<ThermalBag>
        ("SELECT * FROM [ThermalBags] WHERE [SerialNumber] = @SerialNumber AND [IsDeleted] = 0", new { SerialNumber = serialNumber });
    }
}