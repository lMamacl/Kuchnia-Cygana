namespace KuchniaUCygana.Domain.Interfaces.External;

// Dane jednej dostawy na dany dzień - zwracane Modułowi 3
// w celu wygenerowania dziennego planu produkcji.

public sealed record OrderDeliveryInfo(
    int OrderId,
    string OrderNumber,
    int CustomerId,
    string CustomerFullName,
    string AddressFullLine,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude,
    DateTime DeliveryDate,
    string DeliveryWindowName,
    IReadOnlyList<OrderItemInfo> Items);

public sealed record OrderItemInfo(
    int DietId,
    string DietName,
    int DietVariantId,
    string VariantName,
    int CaloriesPerDay);
