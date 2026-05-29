using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class RoutingServiceTests
{
    [Fact]
    public async Task GenerateDailyRoutesAsync_CreatesRoutesWithinVehicleCapacity()
    {
        var date = new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.Zero);
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var stopRepository = new Mock<IDeliveryRouteStopRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var deliveryProvider = new Mock<ILogisticsDeliveryDataProvider>();
        var optimizer = new NearestNeighborRouteOptimizer();
        var insertedRouteIds = new Queue<int>(new[] { 10, 11 });
        var insertedStopIds = new Queue<int>(new[] { 101, 102, 103 });
        var deliveries = new List<LogisticsDeliveryCandidate>
        {
            Delivery(1, "ZAM/1", 53.1325, 23.1688, 6m),
            Delivery(2, "ZAM/2", 53.1400, 23.1700, 4m),
            Delivery(3, "ZAM/3", 53.1200, 23.1600, 4m),
        };

        routeRepository
            .Setup(r => r.GetRoutesWithStopsAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<DeliveryRoute>());
        routeRepository
            .Setup(r => r.InsertAsync(It.IsAny<DeliveryRoute>()))
            .ReturnsAsync(() => insertedRouteIds.Dequeue());
        routeRepository
            .Setup(r => r.UpdateAsync(It.IsAny<DeliveryRoute>()))
            .ReturnsAsync(true);
        stopRepository
            .Setup(r => r.InsertAsync(It.IsAny<DeliveryRouteStop>()))
            .ReturnsAsync(() => insertedStopIds.Dequeue());
        vehicleRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Vehicle>
            {
                new() { Id = 1, RegistrationNumber = "BI1000A", MaxLoadKg = 10m, Status = VehicleStatus.Active },
                new() { Id = 2, RegistrationNumber = "BI2000A", MaxLoadKg = 10m, Status = VehicleStatus.Active },
            });
        deliveryProvider
            .Setup(p => p.GetDeliveriesForDateAsync(date.Date, It.IsAny<decimal>()))
            .ReturnsAsync(deliveries);
        deliveryProvider
            .Setup(p => p.GetDeliveriesByCalendarIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<decimal>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, decimal _) =>
                deliveries.Where(d => ids.Contains(d.DeliveryCalendarId)).ToList());

        var service = new RoutingService(
            routeRepository.Object,
            stopRepository.Object,
            vehicleRepository.Object,
            deliveryProvider.Object,
            optimizer);

        var result = await service.GenerateDailyRoutesAsync(new GenerateDailyRoutesRequest
        {
            RouteDate = date,
            DefaultDeliveryLoadKg = 1m,
        });

        result.Succeeded.Should().BeTrue();
        result.GeneratedRoutesCount.Should().Be(2);
        result.PlannedStopsCount.Should().Be(3);
        result.Routes.SelectMany(r => r.Stops)
            .Select(s => s.DeliveryCalendarId)
            .Should()
            .BeEquivalentTo(new[] { 1, 2, 3 });
        result.Routes.Should().OnlyContain(r => r.VehicleId == 1 || r.VehicleId == 2);
    }

    private static LogisticsDeliveryCandidate Delivery(
        int deliveryCalendarId,
        string orderNumber,
        double latitude,
        double longitude,
        decimal estimatedLoadKg)
    {
        return new LogisticsDeliveryCandidate
        {
            DeliveryCalendarId = deliveryCalendarId,
            OrderId = deliveryCalendarId * 10,
            OrderNumber = orderNumber,
            DeliveryDate = new DateTime(2026, 5, 29),
            FullAddress = $"Adres {deliveryCalendarId}",
            City = "Bialystok",
            PostalCode = "15-001",
            Latitude = latitude,
            Longitude = longitude,
            EstimatedLoadKg = estimatedLoadKg,
        };
    }
}
