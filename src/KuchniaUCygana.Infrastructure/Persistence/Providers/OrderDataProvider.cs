using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.Providers;

// IOrderDataProvider zastępuje OrderDataProviderMock - Moduł 3 dostaje teraz dane z bazy za pomocą Dappera.
public sealed class OrderDataProvider : IOrderDataProvider
{
    private readonly IDbConnectionFactory factory;

    public OrderDataProvider(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
    {
        using var db = factory.CreateConnection();

        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        // 1. Pobierz zaplanowane dostawy na dany dzień za pomocą Dappera
        const string sqlDeliveries = @"
            SELECT * FROM DeliveryCalendar 
            WHERE DeliveryDate >= @DateOnly 
              AND DeliveryDate < @NextDay 
              AND Status = @Status 
              AND IsSkipped = 0 
              AND IsDeleted = 0";

        var deliveries = (await db.QueryAsync<DeliveryCalendar>(sqlDeliveries, new
        {
            DateOnly = dateOnly,
            NextDay = nextDay,
            Status = (int)DeliveryStatus.Scheduled
        })).ToList();

        if (!deliveries.Any())
            return Enumerable.Empty<OrderDeliveryInfo>();

        // 2. Batch load — zbieramy unikalne ID
        var orderIds = deliveries.Select(d => d.OrderId).Distinct().ToList();
        var addressIds = deliveries.Select(d => d.AddressId).Distinct().ToList();
        var windowIds = deliveries
            .Where(d => d.DeliveryWindowId.HasValue)
            .Select(d => d.DeliveryWindowId!.Value)
            .Distinct()
            .ToList();

        // 3. Ładujemy zamówienia
        const string sqlOrders = "SELECT * FROM [Order] WHERE Id IN @OrderIds AND IsDeleted = 0";
        var orders = (await db.QueryAsync<Order>(sqlOrders, new { OrderIds = orderIds }))
            .ToDictionary(o => o.Id);

        // 4. Ładujemy klientów (Users)
        var customerIds = orders.Values.Select(o => o.CustomerId).Distinct().ToList();
        const string sqlCustomers = "SELECT * FROM [User] WHERE Id IN @CustomerIds";
        var customers = (await db.QueryAsync<User>(sqlCustomers, new { CustomerIds = customerIds }))
            .ToDictionary(u => u.Id);

        // 5. Ładujemy adresy
        const string sqlAddresses = "SELECT * FROM Address WHERE Id IN @AddressIds AND IsDeleted = 0";
        var addresses = (await db.QueryAsync<Address>(sqlAddresses, new { AddressIds = addressIds }))
            .ToDictionary(a => a.Id);

        // 6. Ładujemy okna czasowe
        var windows = windowIds.Any()
            ? (await db.QueryAsync<DeliveryWindow>("SELECT * FROM DeliveryWindow WHERE Id IN @WindowIds", new { WindowIds = windowIds }))
                .ToDictionary(w => w.Id)
            : new Dictionary<int, DeliveryWindow>();

        // 7. Ładujemy pozycje zamówień
        const string sqlItems = "SELECT * FROM OrderItem WHERE OrderId IN @OrderIds AND IsDeleted = 0";
        var allItems = (await db.QueryAsync<OrderItem>(sqlItems, new { OrderIds = orderIds }))
            .GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 8. Budujemy wynik (Reszta Twojego kodu bez zmian)
        var result = new List<OrderDeliveryInfo>();

        foreach (var delivery in deliveries)
        {
            if (!orders.TryGetValue(delivery.OrderId, out var order)) continue;
            if (!customers.TryGetValue(order.CustomerId, out var customer)) continue;
            if (!addresses.TryGetValue(delivery.AddressId, out var address)) continue;

            var windowName = delivery.DeliveryWindowId.HasValue &&
                             windows.TryGetValue(delivery.DeliveryWindowId.Value, out var win)
                ? win.Name
                : string.Empty;

            var items = allItems.TryGetValue(delivery.OrderId, out var orderItems)
                ? orderItems.Select(i => new OrderItemInfo(
                    DietId: i.DietId,
                    DietName: i.DietName,
                    DietVariantId: i.DietVariantId,
                    VariantName: i.VariantName,
                    CaloriesPerDay: i.CaloriesPerDay)).ToList()
                : new List<OrderItemInfo>();

            result.Add(new OrderDeliveryInfo(
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                CustomerId: order.CustomerId,
                CustomerFullName: $"{customer.FirstName} {customer.LastName}",
                AddressFullLine: address.FullAddress,
                City: address.City,
                PostalCode: address.PostalCode,
                Latitude: address.Latitude,
                Longitude: address.Longitude,
                DeliveryDate: delivery.DeliveryDate,
                DeliveryWindowName: windowName,
                Items: items));
        }

        return result;
    }
}
