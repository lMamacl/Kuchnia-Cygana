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
        var rng = new Random(seed);

        var orderCount = rng.Next(12, 25); // 12-24 zamówień dziennie
        var orders = new List<ActiveOrderEntry>();

        for (var i = 0; i < orderCount; i++)
        {
            orders.Add(new ActiveOrderEntry
            {
                OrderId = seed * 100 + i + 1,
                ClientId = i + 1,
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
            ClientId = orderId % 16 + 1,
            ClientName = ClientNames[orderId % ClientNames.Length],
            DietVariantId = DietVariantIds[orderId % DietVariantIds.Length],
            DeliveryDate = DateOnly.FromDateTime(DateTime.Today),
        };

        return Task.FromResult<ActiveOrderEntry?>(entry);
    }
}
