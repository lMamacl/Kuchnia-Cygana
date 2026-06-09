using AutoMapper;
using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Application.Services;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class TemperatureServiceTests
{
    [Fact]
    public async Task LogTemperatureAsync_ShouldNotCreateAlert_WhenTemperatureIsInRange()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.LogTemperatureAsync(new LogTemperatureRequest
        {
            HaccpLocationId = 10,
            TemperatureCelsius = 3.5m,
        });

        result.IsOutOfRange.Should().BeFalse();
        fixture.AlertRepository.Verify(r => r.InsertAsync(It.IsAny<HaccpTemperatureAlert>()), Times.Never);
        fixture.AlertRepository.Verify(r => r.UpdateAsync(It.IsAny<HaccpTemperatureAlert>()), Times.Never);
        fixture.NotificationService.Verify(
            s => s.CreateForRolesAsync(It.IsAny<Notification>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task LogTemperatureAsync_ShouldCreateIncidentAndNotification_WhenTemperatureFirstExceedsLimit()
    {
        var fixture = CreateFixture();
        fixture.AlertRepository
            .Setup(r => r.InsertAsync(It.IsAny<HaccpTemperatureAlert>()))
            .ReturnsAsync(77L);

        var result = await fixture.Service.LogTemperatureAsync(new LogTemperatureRequest
        {
            HaccpLocationId = 10,
            TemperatureCelsius = 8.2m,
        });

        result.IsOutOfRange.Should().BeTrue();
        fixture.AlertRepository.Verify(
            r => r.InsertAsync(It.Is<HaccpTemperatureAlert>(a =>
                a.HaccpLocationId == 10
                && a.Status == HaccpTemperatureAlertStatus.Open
                && a.TriggeredTemperatureCelsius == 8.2m)),
            Times.Once);
        fixture.NotificationService.Verify(
            s => s.CreateForRolesAsync(
                It.Is<Notification>(n =>
                    n.Type == "HaccpTemperature"
                    && n.Severity == NotificationSeverity.Danger
                    && n.LinkUrl == "/warehouse/haccp-report?activeTab=Ch%C5%82odnia%20testowa"),
                It.Is<IEnumerable<string>>(roles =>
                    roles.Contains(UserRoles.WarehouseManager)
                    && roles.Contains(UserRoles.Admin)
                    && !roles.Contains(UserRoles.Warehouse))),
            Times.Once);
    }

    [Fact]
    public async Task LogTemperatureAsync_ShouldUpdateOpenIncidentWithoutDuplicateNotification_WhenStillOutOfRange()
    {
        var fixture = CreateFixture(new HaccpTemperatureAlert
        {
            Id = 77,
            HaccpLocationId = 10,
            Status = HaccpTemperatureAlertStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        });

        await fixture.Service.LogTemperatureAsync(new LogTemperatureRequest
        {
            HaccpLocationId = 10,
            TemperatureCelsius = 8.4m,
        });

        fixture.AlertRepository.Verify(
            r => r.UpdateAsync(It.Is<HaccpTemperatureAlert>(a =>
                a.Id == 77
                && a.Status == HaccpTemperatureAlertStatus.Open
                && a.LastTemperatureCelsius == 8.4m)),
            Times.Once);
        fixture.AlertRepository.Verify(r => r.InsertAsync(It.IsAny<HaccpTemperatureAlert>()), Times.Never);
        fixture.NotificationService.Verify(
            s => s.CreateForRolesAsync(It.IsAny<Notification>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task LogTemperatureAsync_ShouldCloseOpenIncident_WhenTemperatureReturnsToRange()
    {
        var fixture = CreateFixture(new HaccpTemperatureAlert
        {
            Id = 77,
            HaccpLocationId = 10,
            Status = HaccpTemperatureAlertStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        });

        await fixture.Service.LogTemperatureAsync(new LogTemperatureRequest
        {
            HaccpLocationId = 10,
            TemperatureCelsius = 2.5m,
        });

        fixture.AlertRepository.Verify(
            r => r.UpdateAsync(It.Is<HaccpTemperatureAlert>(a =>
                a.Id == 77
                && a.Status == HaccpTemperatureAlertStatus.Closed
                && a.ClosedAt.HasValue)),
            Times.Once);
        fixture.NotificationService.Verify(
            s => s.CreateForRolesAsync(It.IsAny<Notification>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    private static TemperatureServiceFixture CreateFixture(HaccpTemperatureAlert? openAlert = null)
    {
        var temperatureLogRepository = new Mock<ITemperatureLogRepository>();
        temperatureLogRepository
            .Setup(r => r.InsertAsync(It.IsAny<TemperatureLog>()))
            .ReturnsAsync(123L);

        var locationRepository = new Mock<IHaccpLocationRepository>();
        locationRepository
            .Setup(r => r.GetDetailsByIdAsync(10))
            .ReturnsAsync(new HaccpLocationDetailsRow
            {
                Id = 10,
                Code = "COLD_TEST",
                Name = "Chłodnia testowa",
                MinTemperatureCelsius = 0m,
                MaxTemperatureCelsius = 4m,
                IsActive = true,
                DisplayOrder = 1,
                CategoryIds = "1,2",
                CategoryNames = "Mięso/Ryby, Nabiał",
            });

        var alertRepository = new Mock<IHaccpTemperatureAlertRepository>();
        alertRepository
            .Setup(r => r.GetOpenByLocationAsync(10))
            .ReturnsAsync(openAlert);

        var notificationService = new Mock<INotificationService>();
        var service = new TemperatureService(
            temperatureLogRepository.Object,
            locationRepository.Object,
            alertRepository.Object,
            notificationService.Object,
            Mock.Of<IMapper>(),
            NullLogger<TemperatureService>.Instance);

        return new TemperatureServiceFixture(
            service,
            alertRepository,
            notificationService);
    }

    private sealed record TemperatureServiceFixture(
        TemperatureService Service,
        Mock<IHaccpTemperatureAlertRepository> AlertRepository,
        Mock<INotificationService> NotificationService);
}
