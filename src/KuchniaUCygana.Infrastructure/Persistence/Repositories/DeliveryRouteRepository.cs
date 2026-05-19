using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium tras dostaw
/// </summary>
public sealed class DeliveryRouteRepository : BaseRepository<DeliveryRoute>, IDeliveryRouteRepository
{
    public DeliveryRouteRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<DeliveryRoute?> GetRouteWithStopsAsync(int routeId)
    {
        using var db = Factory.CreateConnection();
        return await db.SingleByIdAsync<DeliveryRoute>(routeId);
    }
}