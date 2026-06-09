namespace KuchniaUCygana.Domain.Interfaces.External;

// Dane jednej dostawy na dany dzień - zwracane Modułowi 3
// w celu wygenerowania dziennego planu produkcji.

public sealed record OrderDeliveryInfo(
    int DeliveryCalendarId,
    int OrderId,
    string OrderNumber,
    int CustomerId,
    string? ClientPublicId,
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
    int CaloriesPerDay,
    int? MealId = null,
    int? MealVariantId = null,
    int? DietMenuPlanItemId = null,
    string? MealSlot = null);
