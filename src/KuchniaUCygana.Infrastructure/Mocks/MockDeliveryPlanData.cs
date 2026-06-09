using System;

namespace KuchniaUCygana.Infrastructure.Mocks;

internal static class MockDeliveryPlanData
{
    public static Random CreateOrderRandom(DateOnly deliveryDate, out int orderCount)
    {
        var rng = new Random(deliveryDate.DayNumber);
        orderCount = rng.Next(12, 25);
        return rng;
    }

    public static int GetOrderCount(DateOnly deliveryDate)
    {
        var rng = new Random(deliveryDate.DayNumber);
        return rng.Next(12, 25);
    }

    public static int CreateDeliveryCalendarId(DateOnly date, int index)
    {
        return date.DayNumber * 100 + index + 1;
    }

    public static string CreateClientPublicId(int index)
    {
        return $"MC{index + 1:D6}";
    }
}
