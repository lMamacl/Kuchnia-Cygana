using FluentAssertions;
using KuchniaUCygana.Infrastructure.Mocks;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class MockDeliveryPlanProviderTests
{
    [Fact]
    public async Task MockProviders_ShouldReturnMatchingDeliveryCalendarIds_ForSameDate()
    {
        var date = new DateOnly(2035, 6, 1);
        var orderProvider = new MockOrderDataProvider();
        var manifestProvider = new MockDeliveryManifestProvider();

        var orderIds = (await orderProvider.GetActiveOrdersAsync(date))
            .Select(o => o.DeliveryCalendarId)
            .OrderBy(id => id)
            .ToList();

        var stopIds = (await manifestProvider.GetRoutesForDateAsync(date))
            .SelectMany(r => r.Stops)
            .Select(s => s.DeliveryCalendarId)
            .OrderBy(id => id)
            .ToList();

        stopIds.Should().Equal(orderIds);
    }

    [Fact]
    public async Task MockManifestProvider_ShouldAssignEveryMockDeliveryToOnlyOneStop()
    {
        var date = new DateOnly(2035, 6, 2);
        var manifestProvider = new MockDeliveryManifestProvider();

        var routes = (await manifestProvider.GetRoutesForDateAsync(date)).ToList();
        var stops = routes.SelectMany(r => r.Stops).ToList();

        routes.Count.Should().BeInRange(2, 3);
        stops.Select(s => s.DeliveryCalendarId).Should().OnlyHaveUniqueItems();
        stops.Should().OnlyContain(s => s.SequenceNumber > 0);
    }
}
