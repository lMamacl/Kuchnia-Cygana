using Dapper;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Providers;

public sealed class LogisticsDeliveryDataProvider : ILogisticsDeliveryDataProvider
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LogisticsDeliveryDataProvider(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<LogisticsDeliveryCandidate>> GetDeliveriesForDateAsync(
        DateTime deliveryDate,
        decimal defaultDeliveryLoadKg = 1m)
    {
        using var db = _connectionFactory.CreateConnection();
        var from = deliveryDate.Date;
        var to = from.AddDays(1);
        var deliveries = await db.QueryAsync<LogisticsDeliveryCandidate>(
            BaseSql + @"
            WHERE dc.[DeliveryDate] >= @From
              AND dc.[DeliveryDate] < @To
              AND dc.[IsSkipped] = 0
              AND dc.[IsDeleted] = 0
              AND o.[IsDeleted] = 0
              AND a.[IsDeleted] = 0
            " + GroupBySql,
            new { From = from, To = to, DefaultDeliveryLoadKg = defaultDeliveryLoadKg });

        return deliveries.ToList();
    }

    public async Task<IReadOnlyList<LogisticsDeliveryCandidate>> GetDeliveriesByCalendarIdsAsync(
        IReadOnlyCollection<int> deliveryCalendarIds,
        decimal defaultDeliveryLoadKg = 1m)
    {
        if (deliveryCalendarIds.Count == 0)
        {
            return Array.Empty<LogisticsDeliveryCandidate>();
        }

        using var db = _connectionFactory.CreateConnection();
        var deliveries = await db.QueryAsync<LogisticsDeliveryCandidate>(
            BaseSql + @"
            WHERE dc.[Id] IN @DeliveryCalendarIds
              AND dc.[IsDeleted] = 0
              AND o.[IsDeleted] = 0
              AND a.[IsDeleted] = 0
            " + GroupBySql,
            new
            {
                DeliveryCalendarIds = deliveryCalendarIds,
                DefaultDeliveryLoadKg = defaultDeliveryLoadKg,
            });

        return deliveries.ToList();
    }

    private const string BaseSql = @"
        SELECT
            dc.[Id] AS DeliveryCalendarId,
            dc.[OrderId],
            o.[OrderNumber],
            dc.[DeliveryDate],
            CONCAT(
                a.[Street], ' ', a.[BuildingNumber],
                CASE
                    WHEN a.[ApartmentNumber] IS NULL OR a.[ApartmentNumber] = ''
                        THEN ''
                    ELSE CONCAT('/', a.[ApartmentNumber])
                END,
                ', ', a.[PostalCode], ' ', a.[City]
            ) AS FullAddress,
            a.[City],
            a.[PostalCode],
            a.[Latitude],
            a.[Longitude],
            CAST(
                CASE
                    WHEN COUNT(oi.[Id]) = 0 THEN @DefaultDeliveryLoadKg
                    ELSE COUNT(oi.[Id]) * @DefaultDeliveryLoadKg
                END
                AS decimal(18, 2)
            ) AS EstimatedLoadKg
        FROM [DeliveryCalendar] dc
        INNER JOIN [Orders] o ON o.[Id] = dc.[OrderId]
        INNER JOIN [Addresses] a ON a.[Id] = dc.[AddressId]
        LEFT JOIN [OrderItems] oi ON oi.[OrderId] = o.[Id] AND oi.[IsDeleted] = 0
    ";

    private const string GroupBySql = @"
        GROUP BY
            dc.[Id],
            dc.[OrderId],
            o.[OrderNumber],
            dc.[DeliveryDate],
            a.[Street],
            a.[BuildingNumber],
            a.[ApartmentNumber],
            a.[City],
            a.[PostalCode],
            a.[Latitude],
            a.[Longitude]
        ORDER BY dc.[DeliveryDate], dc.[Id];";
}
