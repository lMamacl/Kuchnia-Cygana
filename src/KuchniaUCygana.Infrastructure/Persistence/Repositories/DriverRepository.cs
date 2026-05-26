using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium kierowców
/// </summary>
public sealed class DriverRepository : BaseRepository<Driver>, IDriverRepository
{
    public DriverRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    // Dodatkowe metody specyficzne dla kierowców, np.:
    // public async Task<Driver?> GetByPhoneNumberAsync(string phoneNumber)
    // {
    //     using var db = Factory.CreateConnection();
    //     return await db.SingleAsync<Driver>(d => d.PhoneNumber == phoneNumber && !d.IsDeleted);
    // }
}