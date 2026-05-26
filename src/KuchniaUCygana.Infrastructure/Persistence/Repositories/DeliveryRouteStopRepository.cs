using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

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
        var sql = @"
            SELECT * FROM [DeliveryRouteStops] 
            WHERE [RouteId] = @RouteId 
            AND [IsDeleted] = 0 
            ORDER BY [SequenceNumber]";
        
        var stops = await db.QueryAsync<DeliveryRouteStop>(sql, new { RouteId = routeId });
        return stops;
    }
}