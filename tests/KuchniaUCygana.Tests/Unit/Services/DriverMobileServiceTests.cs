using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class DriverMobileServiceTests
{
    [Fact]
    public async Task ConfirmDeliveryAsync_UpdatesStatusesAcrossModules()
    {
        var scenario = CreateScenario();

        await scenario.Service.ConfirmDeliveryAsync(
            scenario.Stop.Id,
            new ConfirmDriverDeliveryRequest { BagCodes = { scenario.Bag.BagCode } });

        scenario.Stop.Status.Should().Be(StopStatus.Completed);
        scenario.PackingSession.Status.Should().Be(PackingStatus.Delivered);
        scenario.DeliveryCalendar.Status.Should().Be(DeliveryStatus.Delivered);
        scenario.DeliveryCalendar.IsSkipped.Should().BeFalse();
        scenario.Route.Status.Should().Be(RouteStatus.Completed);
        scenario.StopRepository.Verify(repository => repository.UpdateAsync(scenario.Stop), Times.Once);
        scenario.RouteRepository.Verify(repository => repository.UpdateAsync(scenario.Route), Times.Once);
    }

    [Fact]
    public async Task ReportProblemAsync_StoresIssueAndMarksDeliveryAsFailed()
    {
        var scenario = CreateScenario();
        DeliveryIssue? storedIssue = null;
        scenario.IssueRepository
            .Setup(repository => repository.InsertAsync(It.IsAny<DeliveryIssue>()))
            .Callback<DeliveryIssue>(issue => storedIssue = issue)
            .ReturnsAsync(1);

        await scenario.Service.ReportProblemAsync(
            scenario.Stop.Id,
            new ReportDriverDeliveryProblemRequest
            {
                Reason = "Brak odbiorcy",
                Notes = "Telefon nie odpowiada.",
            });

        storedIssue.Should().NotBeNull();
        storedIssue!.RouteStopId.Should().Be(scenario.Stop.Id);
        storedIssue.DriverId.Should().Be(scenario.Driver.Id);
        storedIssue.Reason.Should().Be("Brak odbiorcy");
        scenario.Stop.Status.Should().Be(StopStatus.Failed);
        scenario.PackingSession.Status.Should().Be(PackingStatus.DeliveryFailed);
        scenario.DeliveryCalendar.Status.Should().Be(DeliveryStatus.Skipped);
        scenario.DeliveryCalendar.SkipReason.Should().Be("Brak odbiorcy");
        scenario.Route.Status.Should().Be(RouteStatus.Failed);
    }

    private static Scenario CreateScenario()
    {
        var driver = new Driver { Id = 7, UserId = 11, IsActive = true };
        var vehicle = new Vehicle { Id = 17, RegistrationNumber = "BI1234A", Model = "Fiat Ducato" };
        var stop = new DeliveryRouteStop
        {
            Id = 31,
            RouteId = 21,
            DeliveryCalendarId = 41,
            SequenceNumber = 1,
            Status = StopStatus.InProgress,
        };
        var route = new DeliveryRoute
        {
            Id = 21,
            VehicleId = vehicle.Id,
            RouteDate = DateTimeOffset.Now.Date,
            Status = RouteStatus.InProgress,
            Stops = { stop },
        };
        var packingSession = new PackingSession
        {
            Id = 51,
            PackingDate = DateOnly.FromDateTime(DateTime.Now),
            DeliveryCalendarId = stop.DeliveryCalendarId,
            Status = PackingStatus.Dispatched,
        };
        var bag = new PackingBag
        {
            Id = 61,
            PackingSessionId = packingSession.Id,
            BagCode = "BAG-61",
            Status = PackingBagStatus.Dispatched,
        };
        var deliveryCalendar = new DeliveryCalendar
        {
            Id = stop.DeliveryCalendarId,
            Status = DeliveryStatus.Scheduled,
        };

        var currentUserService = new Mock<ICurrentUserService>();
        var userRepository = new Mock<IUserRepository>();
        var driverRepository = new Mock<IDriverRepository>();
        var assignmentRepository = new Mock<IDriverVehicleAssignmentRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        var routeRepository = new Mock<IDeliveryRouteRepository>();
        var stopRepository = new Mock<IDeliveryRouteStopRepository>();
        var issueRepository = new Mock<IDeliveryIssueRepository>();
        var deliveryCalendarRepository = new Mock<IDeliveryCalendarRepository>();
        var deliveryDataProvider = new Mock<ILogisticsDeliveryDataProvider>();
        var packingSessionRepository = new Mock<IPackingSessionRepository>();
        var packingBagRepository = new Mock<IPackingBagRepository>();
        var manifestRepository = new Mock<IPackingManifestRepository>();

        currentUserService.Setup(service => service.GetUserId()).Returns(driver.UserId);
        currentUserService.Setup(service => service.GetUserName()).Returns("driver@kuchnia.local");
        userRepository
            .Setup(repository => repository.GetByIdAsync(driver.UserId))
            .ReturnsAsync(new User { Id = driver.UserId, FirstName = "Jan", LastName = "Kowalski" });
        driverRepository.Setup(repository => repository.GetByUserIdAsync(driver.UserId)).ReturnsAsync(driver);
        assignmentRepository
            .Setup(repository => repository.GetActiveByDriverIdAsync(driver.Id))
            .ReturnsAsync(new DriverVehicleAssignment { DriverId = driver.Id, VehicleId = vehicle.Id });
        vehicleRepository.Setup(repository => repository.GetByIdAsync(vehicle.Id)).ReturnsAsync(vehicle);
        routeRepository
            .Setup(repository => repository.GetRoutesWithStopsAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<DeliveryRoute> { route });
        issueRepository
            .Setup(repository => repository.GetByRouteStopIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(Array.Empty<DeliveryIssue>());
        deliveryCalendarRepository
            .Setup(repository => repository.GetByIdAsync(deliveryCalendar.Id))
            .ReturnsAsync(deliveryCalendar);
        packingSessionRepository
            .Setup(repository => repository.GetByDateWithItemsAsync(It.IsAny<DateOnly>()))
            .ReturnsAsync(new[] { packingSession });
        packingBagRepository
            .Setup(repository => repository.GetBySessionIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new[] { bag });
        deliveryDataProvider
            .Setup(provider => provider.GetDeliveriesByCalendarIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<decimal>()))
            .ReturnsAsync(new List<LogisticsDeliveryCandidate>
            {
                new()
                {
                    DeliveryCalendarId = stop.DeliveryCalendarId,
                    OrderNumber = "ORD-41",
                    FullAddress = "Testowa 1, Bialystok",
                },
            });

        var service = new DriverMobileService(
            currentUserService.Object,
            userRepository.Object,
            driverRepository.Object,
            assignmentRepository.Object,
            vehicleRepository.Object,
            routeRepository.Object,
            stopRepository.Object,
            issueRepository.Object,
            deliveryCalendarRepository.Object,
            deliveryDataProvider.Object,
            packingSessionRepository.Object,
            packingBagRepository.Object,
            manifestRepository.Object);

        return new Scenario(
            service,
            driver,
            route,
            stop,
            packingSession,
            bag,
            deliveryCalendar,
            routeRepository,
            stopRepository,
            issueRepository);
    }

    private sealed record Scenario(
        DriverMobileService Service,
        Driver Driver,
        DeliveryRoute Route,
        DeliveryRouteStop Stop,
        PackingSession PackingSession,
        PackingBag Bag,
        DeliveryCalendar DeliveryCalendar,
        Mock<IDeliveryRouteRepository> RouteRepository,
        Mock<IDeliveryRouteStopRepository> StopRepository,
        Mock<IDeliveryIssueRepository> IssueRepository);
}
