using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Infrastructure.Mocks;

/// <summary>
/// Mock dostawcy zamówień (Moduł 1 niedostępny).
/// Generuje realistyczne zamówienia na konkretną datę za pomocą deterministycznych danych.
/// Zastąpione adapterem M1 w fazie integracji.
/// </summary>
public sealed class MockOrderDataProvider : IOrderDataProvider
{
    private static readonly string[] ClientNames =
    {
        "Jan Kowalski", "Anna Nowak", "Piotr Wiśniewski", "Maria Wójcik",
        "Tomasz Kamiński", "Katarzyna Lewandowska", "Michał Zieliński",
        "Agnieszka Szymańska", "Krzysztof Woźniak", "Magdalena Dąbrowska",
        "Robert Kozłowski", "Joanna Jankowska", "Andrzej Mazur",
        "Ewa Krawczyk", "Marcin Piotrowski", "Monika Grabowska",
    };

    private static readonly int[] DietVariantIds = { 1, 2, 3, 4, 5 };

    public Task<IEnumerable<ActiveOrderEntry>> GetActiveOrdersAsync(DateOnly deliveryDate)
    {
        // Deterministyczny seed z daty — te same zamówienia dla tej samej daty
        var seed = deliveryDate.DayNumber;
        var rng = MockDeliveryPlanData.CreateOrderRandom(deliveryDate, out var orderCount);

        var orders = new List<ActiveOrderEntry>();

        for (var i = 0; i < orderCount; i++)
        {
            orders.Add(new ActiveOrderEntry
            {
                DeliveryCalendarId = MockDeliveryPlanData.CreateDeliveryCalendarId(deliveryDate, i),
                OrderId = seed * 100 + i + 1,
                ClientId = i + 1,
                ClientPublicId = MockDeliveryPlanData.CreateClientPublicId(i),
                ClientName = ClientNames[i % ClientNames.Length],
                DietVariantId = DietVariantIds[rng.Next(DietVariantIds.Length)],
                DeliveryDate = deliveryDate,
            });
        }

        return Task.FromResult<IEnumerable<ActiveOrderEntry>>(orders);
    }

    public Task<ActiveOrderEntry?> GetOrderByIdAsync(int orderId)
    {
        var entry = new ActiveOrderEntry
        {
            OrderId = orderId,
            DeliveryCalendarId = orderId,
            ClientId = orderId % 16 + 1,
            ClientPublicId = MockDeliveryPlanData.CreateClientPublicId(orderId % 16),
            ClientName = ClientNames[orderId % ClientNames.Length],
            DietVariantId = DietVariantIds[orderId % DietVariantIds.Length],
            DeliveryDate = DateOnly.FromDateTime(DateTime.Today),
        };

        return Task.FromResult<ActiveOrderEntry?>(entry);
    }

    public Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
    {
        var deliveryDate = DateOnly.FromDateTime(date.Date);
        var orders = GetActiveOrdersAsync(deliveryDate).Result.ToList();
        var deliveries = orders.Select((order, index) => new OrderDeliveryInfo(
            order.DeliveryCalendarId,
            order.OrderId,
            $"MOCK-{order.OrderId}",
            order.ClientId,
            order.ClientPublicId,
            order.ClientName,
            $"Warszawa, ul. Przykladowa {index + 1}",
            "Warszawa",
            "00-001",
            52.2297 + index * 0.002,
            21.0122 + index * 0.002,
            deliveryDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(8 + index % 6))),
            $"{8 + index % 6:00}:00-{9 + index % 6:00}:00",
            new[]
            {
                new OrderItemInfo(
                    order.DietVariantId,
                    "Dieta testowa",
                    order.DietVariantId,
                    $"Wariant {order.DietVariantId}",
                    order.DietVariantId switch
                    {
                        2 => 1500,
                        3 => 2500,
                        4 => 1800,
                        _ => 2000,
                    }),
            }));

        return Task.FromResult<IEnumerable<OrderDeliveryInfo>>(deliveries);
    }
}
