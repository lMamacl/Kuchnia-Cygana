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

    public async Task<IReadOnlyList<DeliveryCalendar>> GetByIdsAsync(IEnumerable<int> ids)
    {
        using var db = Factory.CreateConnection();
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<DeliveryCalendar>();
        }

        const string sql = """
            SELECT *
            FROM DeliveryCalendar
            WHERE Id IN @Ids
              AND IsDeleted = 0
            ORDER BY DeliveryDate, Id;
            """;

        var deliveries = await db.QueryAsync<DeliveryCalendar>(sql, new { Ids = idList });
        return deliveries.ToList();
    }

    public async Task<IReadOnlyList<TicketDeliveryOptionLookupRow>> SearchTicketDeliveryOptionsAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        int limit)
    {
        using var db = Factory.CreateConnection();
        var safeLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 500);
        const string sql = """
            SELECT TOP (@Limit)
                delivery.[Id] AS [DeliveryCalendarId],
                orders.[Id] AS [OrderId],
                orders.[CustomerId],
                orders.[OrderNumber],
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CONCAT(customer.[FirstName], ' ', customer.[LastName]))), ''),
                    customer.[Email],
                    CONCAT('Klient #', orders.[CustomerId])) AS [CustomerFullName],
                delivery.[DeliveryDate],
                delivery.[Status] AS [DeliveryStatus],
                CASE
                    WHEN address.[Id] IS NULL THEN CONCAT('Adres #', delivery.[AddressId])
                    WHEN NULLIF(LTRIM(RTRIM(address.[ApartmentNumber])), '') IS NULL
                        THEN CONCAT(address.[Street], ' ', address.[BuildingNumber], ', ', address.[PostalCode], ' ', address.[City])
                    ELSE CONCAT(address.[Street], ' ', address.[BuildingNumber], '/', address.[ApartmentNumber], ', ', address.[PostalCode], ' ', address.[City])
                END AS [AddressFullLine],
                CASE
                    WHEN dietLabels.[Labels] IS NULL THEN ''
                    WHEN dietCount.[TotalLabels] > 2 THEN CONCAT(dietLabels.[Labels], ' +', dietCount.[TotalLabels] - 2)
                    ELSE dietLabels.[Labels]
                END AS [DietSummary]
            FROM [DeliveryCalendar] delivery
            INNER JOIN [Orders] orders ON orders.[Id] = delivery.[OrderId] AND orders.[IsDeleted] = 0
            LEFT JOIN [Users] customer ON customer.[Id] = orders.[CustomerId]
            LEFT JOIN [Addresses] address ON address.[Id] = delivery.[AddressId] AND address.[IsDeleted] = 0
            OUTER APPLY
            (
                SELECT STRING_AGG(topLabels.[Label], ', ') WITHIN GROUP (ORDER BY topLabels.[Label]) AS [Labels]
                FROM
                (
                    SELECT TOP (2) labels.[Label]
                    FROM
                    (
                        SELECT DISTINCT NULLIF(LTRIM(RTRIM(CONCAT(item.[DietName], ' ', item.[VariantName]))), '') AS [Label]
                        FROM [OrderItems] item
                        WHERE item.[OrderId] = orders.[Id]
                          AND item.[IsDeleted] = 0
                    ) labels
                    WHERE labels.[Label] IS NOT NULL
                    ORDER BY labels.[Label]
                ) topLabels
            ) dietLabels
            OUTER APPLY
            (
                SELECT COUNT(1) AS [TotalLabels]
                FROM
                (
                    SELECT DISTINCT NULLIF(LTRIM(RTRIM(CONCAT(item.[DietName], ' ', item.[VariantName]))), '') AS [Label]
                    FROM [OrderItems] item
                    WHERE item.[OrderId] = orders.[Id]
                      AND item.[IsDeleted] = 0
                ) labels
                WHERE labels.[Label] IS NOT NULL
            ) dietCount
            WHERE delivery.[DeliveryDate] >= @FromInclusive
              AND delivery.[DeliveryDate] < @ToExclusive
              AND delivery.[IsDeleted] = 0
            ORDER BY delivery.[DeliveryDate] DESC, delivery.[Id] DESC;
            """;

        var rows = await db.QueryAsync<TicketDeliveryOptionLookupRow>(
            sql,
            new
            {
                FromInclusive = fromInclusive.Date,
                ToExclusive = toExclusive.Date,
                Limit = safeLimit,
            });
        return rows.ToList();
    }

    public async Task<IEnumerable<DeliveryCalendar>> GetByDateRangeAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        using var db = Factory.CreateConnection();
        const string sql = @"
            SELECT *
            FROM DeliveryCalendar
            WHERE DeliveryDate >= @FromInclusive
              AND DeliveryDate < @ToExclusive
              AND IsDeleted = 0
            ORDER BY DeliveryDate DESC, Id DESC";

        return await db.QueryAsync<DeliveryCalendar>(
            sql,
            new
            {
                FromInclusive = fromInclusive.Date,
                ToExclusive = toExclusive.Date,
            });
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


