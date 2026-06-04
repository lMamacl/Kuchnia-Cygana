using FluentAssertions;
using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Services.Logistics;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using Moq;

namespace KuchniaUCygana.Tests.Unit.Services;

public sealed class DriverServiceTests
{
    [Fact]
    public async Task GetAllAsync_IncludesCurrentVehicleAssignment()
    {
        var driver = new Driver { Id = 1, UserId = 10, LicenseNumber = "ABC", IsActive = true };
        var vehicle = new Vehicle { Id = 20, RegistrationNumber = "BI1234A", Model = "Fiat Ducato" };
        var driverRepository = new Mock<IDriverRepository>();
        var assignmentRepository = new Mock<IDriverVehicleAssignmentRepository>();
        var userRepository = new Mock<IUserRepository>();
        var vehicleRepository = new Mock<IVehicleRepository>();
        driverRepository.Setup(repository => repository.GetAllAsync()).ReturnsAsync(new[] { driver });
        assignmentRepository
            .Setup(repository => repository.GetActiveAsync())
            .ReturnsAsync(new[] { new DriverVehicleAssignment { DriverId = driver.Id, VehicleId = vehicle.Id } });
        userRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[] { new User { Id = 10, FirstName = "Jan", LastName = "Kowalski" } });
        vehicleRepository.Setup(repository => repository.GetAllAsync()).ReturnsAsync(new[] { vehicle });

        var result = (await CreateService(
            driverRepository,
            assignmentRepository,
            userRepository,
            vehicleRepository).GetAllAsync()).Single();

        result.CurrentVehicleId.Should().Be(vehicle.Id);
        result.CurrentVehicleRegistration.Should().Be("BI1234A");
        result.CurrentVehicleModel.Should().Be("Fiat Ducato");
    }

    [Fact]
    public async Task AssignVehicleAsync_RejectsInactiveDriver()
    {
        var driverRepository = new Mock<IDriverRepository>();
        var assignmentRepository = new Mock<IDriverVehicleAssignmentRepository>();
        driverRepository
            .Setup(repository => repository.GetByIdAsync(1))
            .ReturnsAsync(new Driver { Id = 1, IsActive = false });

        var action = () => CreateService(driverRepository, assignmentRepository)
            .AssignVehicleAsync(1, 2);

        await action.Should().ThrowAsync<InvalidOperationException>();
        assignmentRepository.Verify(repository => repository.AssignAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UnassignsVehicleWhenDriverBecomesInactive()
    {
        var driver = new Driver { Id = 1, UserId = 10, LicenseNumber = "ABC", IsActive = true };
        var driverRepository = new Mock<IDriverRepository>();
        var assignmentRepository = new Mock<IDriverVehicleAssignmentRepository>();
        var userRepository = new Mock<IUserRepository>();
        driverRepository.Setup(repository => repository.GetByIdAsync(1)).ReturnsAsync(driver);
        driverRepository
            .Setup(repository => repository.GetByLicenseNumberAsync("ABC"))
            .ReturnsAsync(driver);
        driverRepository.Setup(repository => repository.UpdateAsync(driver)).ReturnsAsync(true);

        await CreateService(driverRepository, assignmentRepository, userRepository)
            .UpdateAsync(1, new UpdateDriverRequest
            {
                Id = 1,
                UserId = 10,
                LicenseNumber = "ABC",
                IsActive = false,
            });

        assignmentRepository.Verify(repository => repository.UnassignDriverAsync(1), Times.Once);
    }

    private static DriverService CreateService(
        Mock<IDriverRepository> driverRepository,
        Mock<IDriverVehicleAssignmentRepository> assignmentRepository,
        Mock<IUserRepository>? userRepository = null,
        Mock<IVehicleRepository>? vehicleRepository = null)
    {
        return new DriverService(
            driverRepository.Object,
            assignmentRepository.Object,
            (userRepository ?? new Mock<IUserRepository>()).Object,
            (vehicleRepository ?? new Mock<IVehicleRepository>()).Object);
    }
}
