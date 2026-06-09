using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
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
        var vehicles = new List<Vehicle>
        {
            new() { Id = 1, RegistrationNumber = "BI1000A", MaxLoadKg = 10m, Status = VehicleStatus.Active },
            new() { Id = 2, RegistrationNumber = "BI2000A", MaxLoadKg = 10m, Status = VehicleStatus.Active },
        };
        vehicleRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(vehicles);
        vehicleRepository
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(vehicles);
        vehicleRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) => vehicles.Where(vehicle => ids.Contains(vehicle.Id)).ToList());
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
            new Mock<IDriverRepository>().Object,
            EmptyAssignmentRepository().Object,
            new Mock<IUserRepository>().Object,
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

    [Fact]
    public async Task GenerateDailyRoutesAsync_SplitsDistantClustersAcrossAvailableVehicles()
    {
        var date = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var deliveries = new List<LogisticsDeliveryCandidate>
        {
            Delivery(1, "ZAM/1", 53.1325, 23.1688, 1m),
            Delivery(2, "ZAM/2", 53.1400, 23.1700, 1m),
            Delivery(3, "ZAM/3", 52.2297, 21.0122, 1m),
            Delivery(4, "ZAM/4", 52.2350, 21.0200, 1m),
        };
        var service = CreateGenerationService(deliveries, TwoActiveVehicles());

        var result = await service.GenerateDailyRoutesAsync(new GenerateDailyRoutesRequest
        {
            RouteDate = date,
        });

        result.Succeeded.Should().BeTrue();
        result.Routes.Should().HaveCount(2);
        result.Routes.Should().ContainSingle(route =>
            route.Stops.Select(stop => stop.DeliveryCalendarId).Order().SequenceEqual(new[] { 1, 2 }));
        result.Routes.Should().ContainSingle(route =>
            route.Stops.Select(stop => stop.DeliveryCalendarId).Order().SequenceEqual(new[] { 3, 4 }));
    }

    [Fact]
    public async Task GenerateDailyRoutesAsync_KeepsNearbyStopsInSingleRouteWhenCapacityAllows()
    {
        var date = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var deliveries = new List<LogisticsDeliveryCandidate>
        {
            Delivery(1, "ZAM/1", 53.1325, 23.1688, 1m),
            Delivery(2, "ZAM/2", 53.1400, 23.1700, 1m),
            Delivery(3, "ZAM/3", 53.1250, 23.1600, 1m),
            Delivery(4, "ZAM/4", 53.1290, 23.1800, 1m),
        };
        var service = CreateGenerationService(deliveries, TwoActiveVehicles());

        var result = await service.GenerateDailyRoutesAsync(new GenerateDailyRoutesRequest
        {
            RouteDate = date,
        });

        result.Succeeded.Should().BeTrue();
        result.Routes.Should().ContainSingle();
        result.Routes.Single().Stops.Should().HaveCount(4);
    }

    [Fact]
    public async Task UpdateRouteAsync_UpdatesVehicleAndStopSequence()
    {
        var route = new DeliveryRoute
        {
            Id = 10,
            Name = "Trasa 1",
            Status = RouteStatus.Assigned,
            VehicleId = 1,
            Stops =
            {
                new DeliveryRouteStop { Id = 101, RouteId = 10, DeliveryCalendarId = 1, SequenceNumber = 1 },
                new DeliveryRouteStop { Id = 102, RouteId = 10, DeliveryCalendarId = 2, SequenceNumber = 2 },
            },
        };
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var stopRepository = new Mock<IDeliveryRouteStopRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var deliveryProvider = new Mock<ILogisticsDeliveryDataProvider>();
        routeRepository.Setup(r => r.GetRouteWithStopsAsync(10)).ReturnsAsync(route);
        routeRepository.Setup(r => r.UpdateAsync(route)).ReturnsAsync(true);
        stopRepository.Setup(r => r.UpdateAsync(It.IsAny<DeliveryRouteStop>())).ReturnsAsync(true);
        vehicleRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(
            new Vehicle { Id = 2, RegistrationNumber = "BI2000A", Status = VehicleStatus.Active });
        vehicleRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(
            (IReadOnlyCollection<int> ids) => new List<Vehicle> { new() { Id = 2, RegistrationNumber = "BI2000A", Status = VehicleStatus.Active } }
                .Where(vehicle => ids.Contains(vehicle.Id))
                .ToList());
        deliveryProvider
            .Setup(p => p.GetDeliveriesByCalendarIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<decimal>()))
            .ReturnsAsync(new List<LogisticsDeliveryCandidate>
            {
                Delivery(1, "ZAM/1", 53.1325, 23.1688, 1m),
                Delivery(2, "ZAM/2", 53.1400, 23.1700, 1m),
            });

        var service = new RoutingService(
            routeRepository.Object,
            stopRepository.Object,
            vehicleRepository.Object,
            new Mock<IDriverRepository>().Object,
            EmptyAssignmentRepository().Object,
            new Mock<IUserRepository>().Object,
            deliveryProvider.Object,
            new NearestNeighborRouteOptimizer());

        var result = await service.UpdateRouteAsync(new UpdateDeliveryRouteRequest
        {
            Id = 10,
            Name = "Trasa Polnoc",
            VehicleId = 2,
            Stops =
            {
                new UpdateDeliveryRouteStopRequest { StopId = 101, SequenceNumber = 2 },
                new UpdateDeliveryRouteStopRequest { StopId = 102, SequenceNumber = 1 },
            },
        });

        result.Should().NotBeNull();
        route.Name.Should().Be("Trasa Polnoc");
        route.VehicleId.Should().Be(2);
        route.Stops.Single(stop => stop.Id == 101).SequenceNumber.Should().Be(2);
        route.Stops.Single(stop => stop.Id == 102).SequenceNumber.Should().Be(1);
        stopRepository.Verify(r => r.UpdateAsync(It.IsAny<DeliveryRouteStop>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateRouteAsync_AssignsActiveDriverAndMapsDriverName()
    {
        var route = new DeliveryRoute
        {
            Id = 10,
            Name = "Trasa 1",
            Status = RouteStatus.Assigned,
            VehicleId = 1,
            Stops =
            {
                new DeliveryRouteStop { Id = 101, RouteId = 10, DeliveryCalendarId = 1, SequenceNumber = 1 },
            },
        };
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var stopRepository = new Mock<IDeliveryRouteStopRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var driverRepository = new Mock<IDriverRepository>();
        var userRepository = new Mock<IUserRepository>();
        var deliveryProvider = new Mock<ILogisticsDeliveryDataProvider>();
        routeRepository.Setup(r => r.GetRouteWithStopsAsync(10)).ReturnsAsync(route);
        routeRepository.Setup(r => r.UpdateAsync(route)).ReturnsAsync(true);
        stopRepository.Setup(r => r.UpdateAsync(It.IsAny<DeliveryRouteStop>())).ReturnsAsync(true);
        vehicleRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(
            new Vehicle { Id = 2, RegistrationNumber = "BI2000A", Status = VehicleStatus.Active });
        vehicleRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(
            (IReadOnlyCollection<int> ids) => new List<Vehicle> { new() { Id = 2, RegistrationNumber = "BI2000A", Status = VehicleStatus.Active } }
                .Where(vehicle => ids.Contains(vehicle.Id))
                .ToList());
        driverRepository.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(
            new Driver { Id = 7, UserId = 70, LicenseNumber = "M4-001", IsActive = true });
        driverRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(
            (IReadOnlyCollection<int> ids) => new List<Driver> { new() { Id = 7, UserId = 70, LicenseNumber = "M4-001", IsActive = true } }
                .Where(driver => ids.Contains(driver.Id))
                .ToList());
        userRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(
            (IReadOnlyCollection<int> ids) => new List<User> { new() { Id = 70, FirstName = "Jan", LastName = "Kierowca" } }
                .Where(user => ids.Contains(user.Id))
                .ToList());
        deliveryProvider
            .Setup(p => p.GetDeliveriesByCalendarIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<decimal>()))
            .ReturnsAsync(new List<LogisticsDeliveryCandidate>
            {
                Delivery(1, "ZAM/1", 53.1325, 23.1688, 1m),
            });

        var service = new RoutingService(
            routeRepository.Object,
            stopRepository.Object,
            vehicleRepository.Object,
            driverRepository.Object,
            EmptyAssignmentRepository().Object,
            userRepository.Object,
            deliveryProvider.Object,
            new NearestNeighborRouteOptimizer());

        var result = await service.UpdateRouteAsync(new UpdateDeliveryRouteRequest
        {
            Id = 10,
            Name = "Trasa Polnoc",
            VehicleId = 2,
            DriverId = 7,
            Stops = { new UpdateDeliveryRouteStopRequest { StopId = 101, SequenceNumber = 1 } },
        });

        route.DriverId.Should().Be(7);
        result!.DriverName.Should().Be("Jan Kierowca");
    }

    [Fact]
    public async Task UpdateRouteAsync_RejectsInactiveDriver()
    {
        var route = new DeliveryRoute
        {
            Id = 10,
            Name = "Trasa 1",
            Status = RouteStatus.Assigned,
            VehicleId = 1,
            Stops =
            {
                new DeliveryRouteStop { Id = 101, RouteId = 10, DeliveryCalendarId = 1, SequenceNumber = 1 },
            },
        };
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var driverRepository = new Mock<IDriverRepository>();
        routeRepository.Setup(r => r.GetRouteWithStopsAsync(10)).ReturnsAsync(route);
        vehicleRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(
            new Vehicle { Id = 2, RegistrationNumber = "BI2000A", Status = VehicleStatus.Active });
        driverRepository.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(
            new Driver { Id = 7, UserId = 70, LicenseNumber = "M4-001", IsActive = false });

        var service = new RoutingService(
            routeRepository.Object,
            new Mock<IDeliveryRouteStopRepository>().Object,
            vehicleRepository.Object,
            driverRepository.Object,
            EmptyAssignmentRepository().Object,
            new Mock<IUserRepository>().Object,
            new Mock<ILogisticsDeliveryDataProvider>().Object,
            new NearestNeighborRouteOptimizer());

        var action = () => service.UpdateRouteAsync(new UpdateDeliveryRouteRequest
        {
            Id = 10,
            Name = "Trasa Polnoc",
            VehicleId = 2,
            DriverId = 7,
            Stops = { new UpdateDeliveryRouteStopRequest { StopId = 101, SequenceNumber = 1 } },
        });

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteRouteAsync_SoftDeletesStopsBeforeRoute()
    {
        var route = new DeliveryRoute
        {
            Id = 10,
            Status = RouteStatus.Assigned,
            Stops =
            {
                new DeliveryRouteStop { Id = 101, RouteId = 10, DeliveryCalendarId = 1, SequenceNumber = 1 },
                new DeliveryRouteStop { Id = 102, RouteId = 10, DeliveryCalendarId = 2, SequenceNumber = 2 },
            },
        };
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var stopRepository = new Mock<IDeliveryRouteStopRepository>();
        routeRepository.Setup(r => r.GetRouteWithStopsAsync(10)).ReturnsAsync(route);
        routeRepository.Setup(r => r.DeleteAsync(10)).ReturnsAsync(true);
        stopRepository.Setup(r => r.DeleteAsync(It.IsAny<int>())).ReturnsAsync(true);

        var service = new RoutingService(
            routeRepository.Object,
            stopRepository.Object,
            new Mock<IVehicleRepository>().Object,
            new Mock<IDriverRepository>().Object,
            EmptyAssignmentRepository().Object,
            new Mock<IUserRepository>().Object,
            new Mock<ILogisticsDeliveryDataProvider>().Object,
            new NearestNeighborRouteOptimizer());

        var deleted = await service.DeleteRouteAsync(10);

        deleted.Should().BeTrue();
        stopRepository.Verify(r => r.DeleteAsync(101), Times.Once);
        stopRepository.Verify(r => r.DeleteAsync(102), Times.Once);
        routeRepository.Verify(r => r.DeleteAsync(10), Times.Once);
    }

    [Fact]
    public async Task GetRoutesForDateAsync_MapsDriverFromActiveVehicleAssignment()
    {
        var date = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);
        var route = new DeliveryRoute
        {
            Id = 10,
            Name = "Trasa 1",
            Status = RouteStatus.Assigned,
            VehicleId = 3,
            DriverId = null,
            Stops =
            {
                new DeliveryRouteStop { Id = 101, RouteId = 10, DeliveryCalendarId = 1, SequenceNumber = 1 },
            },
        };
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var driverRepository = new Mock<IDriverRepository>();
        var assignmentRepository = new Mock<IDriverVehicleAssignmentRepository>();
        var userRepository = new Mock<IUserRepository>();
        var deliveryProvider = new Mock<ILogisticsDeliveryDataProvider>();

        routeRepository
            .Setup(repository => repository.GetRoutesWithStopsAsync(date))
            .ReturnsAsync(new List<DeliveryRoute> { route });
        vehicleRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) =>
                new List<Vehicle> { new() { Id = 3, RegistrationNumber = "BI 3307E", Status = VehicleStatus.Active } }
                    .Where(vehicle => ids.Contains(vehicle.Id))
                    .ToList());
        assignmentRepository
            .Setup(repository => repository.GetActiveByVehicleIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) =>
                new List<DriverVehicleAssignment> { new() { DriverId = 3, VehicleId = 3, AssignedAt = date } }
                    .Where(assignment => ids.Contains(assignment.VehicleId))
                    .ToList());
        driverRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) =>
                new List<Driver> { new() { Id = 3, UserId = 30, LicenseNumber = "DRV-3", IsActive = true } }
                    .Where(driver => ids.Contains(driver.Id))
                    .ToList());
        userRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) =>
                new List<User> { new() { Id = 30, FirstName = "Ewa", LastName = "Wysocka" } }
                    .Where(user => ids.Contains(user.Id))
                    .ToList());
        deliveryProvider
            .Setup(provider => provider.GetDeliveriesByCalendarIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<decimal>()))
            .ReturnsAsync(new List<LogisticsDeliveryCandidate>
            {
                Delivery(1, "ZAM/1", 53.1325, 23.1688, 1m),
            });

        var service = new RoutingService(
            routeRepository.Object,
            new Mock<IDeliveryRouteStopRepository>().Object,
            vehicleRepository.Object,
            driverRepository.Object,
            assignmentRepository.Object,
            userRepository.Object,
            deliveryProvider.Object,
            new NearestNeighborRouteOptimizer());

        var routes = await service.GetRoutesForDateAsync(date);

        routes.Should().ContainSingle();
        routes.Single().DriverId.Should().Be(3);
        routes.Single().DriverName.Should().Be("Ewa Wysocka");
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

    private static RoutingService CreateGenerationService(
        IReadOnlyList<LogisticsDeliveryCandidate> deliveries,
        IReadOnlyList<Vehicle> vehicles)
    {
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var deliveryProvider = new Mock<ILogisticsDeliveryDataProvider>();

        routeRepository
            .Setup(repository => repository.GetRoutesWithStopsAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<DeliveryRoute>());
        routeRepository
            .Setup(repository => repository.InsertManyWithStopsAsync(It.IsAny<IReadOnlyCollection<DeliveryRoute>>()))
            .Returns(Task.CompletedTask);
        vehicleRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(vehicles);
        vehicleRepository
            .Setup(repository => repository.GetActiveAsync())
            .ReturnsAsync(vehicles);
        vehicleRepository
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) => vehicles.Where(vehicle => ids.Contains(vehicle.Id)).ToList());
        deliveryProvider
            .Setup(provider => provider.GetDeliveriesForDateAsync(It.IsAny<DateTime>(), It.IsAny<decimal>()))
            .ReturnsAsync(deliveries);
        deliveryProvider
            .Setup(provider => provider.GetDeliveriesByCalendarIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<decimal>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, decimal _) =>
                deliveries.Where(delivery => ids.Contains(delivery.DeliveryCalendarId)).ToList());

        return new RoutingService(
            routeRepository.Object,
            new Mock<IDeliveryRouteStopRepository>().Object,
            vehicleRepository.Object,
            new Mock<IDriverRepository>().Object,
            EmptyAssignmentRepository().Object,
            new Mock<IUserRepository>().Object,
            deliveryProvider.Object,
            new NearestNeighborRouteOptimizer());
    }

    private static Mock<IDriverVehicleAssignmentRepository> EmptyAssignmentRepository()
    {
        var repository = new Mock<IDriverVehicleAssignmentRepository>();
        repository
            .Setup(r => r.GetActiveByVehicleIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(Array.Empty<DriverVehicleAssignment>());
        return repository;
    }

    private static IReadOnlyList<Vehicle> TwoActiveVehicles()
    {
        return new List<Vehicle>
        {
            new() { Id = 1, RegistrationNumber = "BI1000A", MaxLoadKg = 100m, Status = VehicleStatus.Active },
            new() { Id = 2, RegistrationNumber = "BI2000A", MaxLoadKg = 100m, Status = VehicleStatus.Active },
        };
    }
}
