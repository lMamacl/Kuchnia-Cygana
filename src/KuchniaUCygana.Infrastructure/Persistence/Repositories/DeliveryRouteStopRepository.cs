using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium przystanków tras dostaw
/// </summary>
public sealed class DeliveryRouteStopRepository : BaseRepository<DeliveryRouteStop>, IDeliveryRouteStopRepository
{
    public DeliveryRouteStopRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<IEnumerable<DeliveryRouteStop>> GetStopsForRouteAsync(int routeId)
    {
        using var db = Factory.CreateConnection();
        var stops = await db.SelectAsync<DeliveryRouteStop>(
            q => q.RouteId == routeId && 
                 q.IsDeleted == false);
        return stops.OrderBy(s => s.SequenceNumber);
    }
}