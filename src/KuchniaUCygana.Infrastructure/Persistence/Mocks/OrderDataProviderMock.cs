using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Infrastructure.Persistence.Mocks;

// Tymczasowy mock - zwraca statyczne dane testowe dla Modułu 3.
// Zostanie zastąpiony przez OrderDataProvider (legitna implementacje) w FAZIE 5 integracji.

public sealed class OrderDataProviderMock : IOrderDataProvider
{
    public Task<IEnumerable<OrderDeliveryInfo>> GetDeliveriesForDateAsync(DateTime date)
    {
        var mockData = new List<OrderDeliveryInfo>
        {
            new(
                OrderId: 1,
                OrderNumber: "ORD-MOCK-0001",
                CustomerId: 1,
                CustomerFullName: "Jan Kowalski",
                AddressFullLine: "ul. Testowa 1, 85-001 Bydgoszcz",
                City: "Bydgoszcz",
                PostalCode: "85-001",
                Latitude: null,
                Longitude: null,
                DeliveryDate: date,
                DeliveryWindowName: "6:00-10:00",
                Items: new List<OrderItemInfo>
                {
                    new(DietId: 1, DietName: "Dieta Standard", DietVariantId: 1,
                        VariantName: "1800 kcal", CaloriesPerDay: 1800),
                }),
        };
        return Task.FromResult<IEnumerable<OrderDeliveryInfo>>(mockData);
    }
}
