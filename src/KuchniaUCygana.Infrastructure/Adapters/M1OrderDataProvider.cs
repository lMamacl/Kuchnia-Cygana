using Dapper;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Adapters;

/// <summary>
/// Adapter Modulu 1 oparty o istniejace tabele zamowien.
/// </summary>
public sealed class M1OrderDataProvider : IOrderDataProvider
{
    private readonly IDbConnectionFactory connectionFactory;

    public M1OrderDataProvider(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<ActiveOrderEntry>> GetActiveOrdersAsync(DateOnly deliveryDate)
    {
        using var db = connectionFactory.CreateConnection();
        var from = deliveryDate.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);

        var rows = await db.QueryAsync<ActiveOrderRow>(
            ActiveOrdersSql,
            new
            {
                from,
                to,
                paidStatus = (int)OrderStatus.Paid,
                inProductionStatus = (int)OrderStatus.InProduction,
                scheduledStatus = (int)DeliveryStatus.Scheduled,
            });

        return rows.Select(row => new ActiveOrderEntry
        {
            OrderId = row.OrderId,
            ClientId = row.ClientId,
            ClientName = row.ClientName,
            DietVariantId = row.DietVariantId,
            DeliveryDate = DateOnly.FromDateTime(row.DeliveryDate),
        });
    }

    public async Task<ActiveOrderEntry?> GetOrderByIdAsync(int orderId)
    {
        using var db = connectionFactory.CreateConnection();

        var row = await db.QuerySingleOrDefaultAsync<ActiveOrderRow>(
            ActiveOrderByIdSql,
            new
            {
                orderId,
                paidStatus = (int)OrderStatus.Paid,
                inProductionStatus = (int)OrderStatus.InProduction,
                scheduledStatus = (int)DeliveryStatus.Scheduled,
            });

        if (row is null)
        {
            return null;
        }

        return new ActiveOrderEntry
        {
            OrderId = row.OrderId,
            ClientId = row.ClientId,
            ClientName = row.ClientName,
            DietVariantId = row.DietVariantId,
            DeliveryDate = DateOnly.FromDateTime(row.DeliveryDate),
        };
    }

    public async Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
    {
        using var db = connectionFactory.CreateConnection();
        var from = date.Date;
        var to = from.AddDays(1);

        var deliveries = (await db.QueryAsync<OrderDeliveryRow>(
            DeliveriesForDateSql,
            new
            {
                from,
                to,
                paidStatus = (int)OrderStatus.Paid,
                inProductionStatus = (int)OrderStatus.InProduction,
                scheduledStatus = (int)DeliveryStatus.Scheduled,
            })).ToList();

        var result = new List<OrderDeliveryInfo>(deliveries.Count);

        foreach (var delivery in deliveries)
        {
            var items = await db.QueryAsync<OrderItemRow>(
                OrderItemsForDeliverySql,
                new { orderId = delivery.OrderId });

            result.Add(new OrderDeliveryInfo(
                delivery.OrderId,
                delivery.OrderNumber,
                delivery.CustomerId,
                delivery.CustomerFullName,
                delivery.AddressFullLine,
                delivery.City,
                delivery.PostalCode,
                delivery.Latitude,
                delivery.Longitude,
                delivery.DeliveryDate,
                delivery.DeliveryWindowName ?? string.Empty,
                items.Select(item => new OrderItemInfo(
                    item.DietId,
                    item.DietName,
                    item.DietVariantId,
                    item.VariantName,
                    item.CaloriesPerDay)).ToList()));
        }

        return result;
    }

    private const string ActiveOrdersSql = """
        SELECT
            o.Id AS OrderId,
            u.Id AS ClientId,
            CONCAT(u.FirstName, ' ', u.LastName) AS ClientName,
            oi.DietVariantId AS DietVariantId,
            dc.DeliveryDate AS DeliveryDate
        FROM DeliveryCalendar dc
        INNER JOIN Orders o ON o.Id = dc.OrderId
        INNER JOIN OrderItems oi ON oi.OrderId = o.Id
        INNER JOIN Users u ON u.Id = o.CustomerId
        WHERE dc.DeliveryDate >= @from
          AND dc.DeliveryDate < @to
          AND o.Status IN (@paidStatus, @inProductionStatus)
          AND dc.Status = @scheduledStatus
          AND dc.IsSkipped = 0
          AND o.IsDeleted = 0
          AND oi.IsDeleted = 0
          AND dc.IsDeleted = 0
        ORDER BY o.Id, oi.Id;
        """;

    private const string ActiveOrderByIdSql = """
        SELECT TOP 1
            o.Id AS OrderId,
            u.Id AS ClientId,
            CONCAT(u.FirstName, ' ', u.LastName) AS ClientName,
            oi.DietVariantId AS DietVariantId,
            dc.DeliveryDate AS DeliveryDate
        FROM Orders o
        INNER JOIN OrderItems oi ON oi.OrderId = o.Id
        INNER JOIN DeliveryCalendar dc ON dc.OrderId = o.Id
        INNER JOIN Users u ON u.Id = o.CustomerId
        WHERE o.Id = @orderId
          AND o.Status IN (@paidStatus, @inProductionStatus)
          AND dc.Status = @scheduledStatus
          AND dc.IsSkipped = 0
          AND o.IsDeleted = 0
          AND oi.IsDeleted = 0
          AND dc.IsDeleted = 0
        ORDER BY dc.DeliveryDate, oi.Id;
        """;

    private const string DeliveriesForDateSql = """
        SELECT
            o.Id AS OrderId,
            o.OrderNumber AS OrderNumber,
            u.Id AS CustomerId,
            CONCAT(u.FirstName, ' ', u.LastName) AS CustomerFullName,
            CASE
                WHEN a.ApartmentNumber IS NULL OR a.ApartmentNumber = ''
                    THEN CONCAT(a.Street, ' ', a.BuildingNumber, ', ', a.PostalCode, ' ', a.City)
                ELSE CONCAT(a.Street, ' ', a.BuildingNumber, '/', a.ApartmentNumber, ', ', a.PostalCode, ' ', a.City)
            END AS AddressFullLine,
            a.City AS City,
            a.PostalCode AS PostalCode,
            a.Latitude AS Latitude,
            a.Longitude AS Longitude,
            dc.DeliveryDate AS DeliveryDate,
            dw.Name AS DeliveryWindowName
        FROM DeliveryCalendar dc
        INNER JOIN Orders o ON o.Id = dc.OrderId
        INNER JOIN Users u ON u.Id = o.CustomerId
        INNER JOIN Addresses a ON a.Id = dc.AddressId
        LEFT JOIN DeliveryWindows dw ON dw.Id = dc.DeliveryWindowId
        WHERE dc.DeliveryDate >= @from
          AND dc.DeliveryDate < @to
          AND o.Status IN (@paidStatus, @inProductionStatus)
          AND dc.Status = @scheduledStatus
          AND dc.IsSkipped = 0
          AND o.IsDeleted = 0
          AND dc.IsDeleted = 0
          AND a.IsDeleted = 0
        ORDER BY dc.DeliveryDate, o.Id;
        """;

    private const string OrderItemsForDeliverySql = """
        SELECT
            DietId,
            DietName,
            DietVariantId,
            VariantName,
            CaloriesPerDay
        FROM OrderItems
        WHERE OrderId = @orderId
          AND IsDeleted = 0
        ORDER BY Id;
        """;

    private sealed class ActiveOrderRow
    {
        public int OrderId { get; set; }

        public int ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public int DietVariantId { get; set; }

        public DateTime DeliveryDate { get; set; }
    }

    private sealed class OrderDeliveryRow
    {
        public int OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public string CustomerFullName { get; set; } = string.Empty;

        public string AddressFullLine { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public DateTime DeliveryDate { get; set; }

        public string? DeliveryWindowName { get; set; }
    }

    private sealed class OrderItemRow
    {
        public int DietId { get; set; }

        public string DietName { get; set; } = string.Empty;

        public int DietVariantId { get; set; }

        public string VariantName { get; set; } = string.Empty;

        public int CaloriesPerDay { get; set; }
    }
}
