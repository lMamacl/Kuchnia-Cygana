using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DeliveryCalendarRepository : BaseRepository<DeliveryCalendar>, IDeliveryCalendarRepository
{
    public DeliveryCalendarRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    public async Task<IEnumerable<DeliveryCalendar>> GetByOrderIdAsync(int orderId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM DeliveryCalendar WHERE OrderId = @OrderId AND IsDeleted = 0 ORDER BY DeliveryDate";
        return await db.QueryAsync<DeliveryCalendar>(sql, new { OrderId = orderId });
    }

    public async Task<IEnumerable<DeliveryCalendar>> GetScheduledForDateAsync(DateTime date)
    {
        using var db = Factory.CreateConnection();
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        const string sql = @"
            SELECT * FROM DeliveryCalendar 
            WHERE DeliveryDate >= @DateOnly 
              AND DeliveryDate < @NextDay 
              AND Status = @Status 
              AND IsSkipped = 0 
              AND IsDeleted = 0";

        return await db.QueryAsync<DeliveryCalendar>(sql, new
        {
            DateOnly = dateOnly,
            NextDay = nextDay,
            Status = (int)DeliveryStatus.Scheduled
        });
    }

    public Task<bool> IsDateAvailableAsync(DateTime date)
    {
        return Task.FromResult(true);
    }
}


