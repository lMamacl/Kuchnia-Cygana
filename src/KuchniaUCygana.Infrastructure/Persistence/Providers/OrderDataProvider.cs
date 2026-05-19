using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Providers;

// IOrderDataProvider zastepuje OrderDataProviderMock - Modul 3 dostaje teraz dane z bazy.
// Uzywa bezposredniego dostepu do DB (batch loading)

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

        // 1. Pobierz zaplanowane dostawy na dany dzien
        var deliveries = await db.SelectAsync<DeliveryCalendar>(x =>
            x.DeliveryDate >= dateOnly &&
            x.DeliveryDate < nextDay &&
            x.Status == DeliveryStatus.Scheduled &&
            x.IsSkipped == false &&
            x.IsDeleted == false);

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

        // 3. Ladujemy zamowienia
        var orders = (await db.SelectAsync<Order>(x =>
            Sql.In(x.Id, orderIds) && x.IsDeleted == false))
            .ToDictionary(o => o.Id);

        // 4. Ladujemy klientow (Users)
        var customerIds = orders.Values.Select(o => o.CustomerId).Distinct().ToList();
        var customers = (await db.SelectAsync<User>(x =>
            Sql.In(x.Id, customerIds)))
            .ToDictionary(u => u.Id);

        // 5. Ladujemy adresy
        var addresses = (await db.SelectAsync<Address>(x =>
            Sql.In(x.Id, addressIds) && x.IsDeleted == false))
            .ToDictionary(a => a.Id);

        // 6. Ladujemy okna czasowe
        var windows = windowIds.Any()
            ? (await db.SelectAsync<DeliveryWindow>(x => Sql.In(x.Id, windowIds)))
                .ToDictionary(w => w.Id)
            : new Dictionary<int, DeliveryWindow>();

        // 7. Ladujemy pozycje zamowien
        var allItems = (await db.SelectAsync<OrderItem>(x =>
            Sql.In(x.OrderId, orderIds) && x.IsDeleted == false))
            .GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 8. Budujemy wynik
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
