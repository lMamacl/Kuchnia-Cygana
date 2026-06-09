using KuchniaUCygana.Application.DTOs.Logistics;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

public sealed class DriverService : IDriverService
{
    private readonly IDriverRepository _driverRepository;
    private readonly IDriverVehicleAssignmentRepository _assignmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public DriverService(
        IDriverRepository driverRepository,
        IDriverVehicleAssignmentRepository assignmentRepository,
        IUserRepository userRepository,
        IVehicleRepository vehicleRepository)
    {
        _driverRepository = driverRepository;
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<IEnumerable<DriverDto>> GetAllAsync()
    {
        var drivers = (await _driverRepository.GetAllAsync()).ToList();
        var users = (await _userRepository.GetAllAsync()).ToDictionary(user => user.Id);
        var assignments = (await _assignmentRepository.GetActiveAsync()).ToDictionary(assignment => assignment.DriverId);
        var vehicles = (await _vehicleRepository.GetAllAsync()).ToDictionary(vehicle => vehicle.Id);

        return drivers
            .Select(driver => Map(
                driver,
                users.GetValueOrDefault(driver.UserId),
                assignments.GetValueOrDefault(driver.Id),
                assignments.TryGetValue(driver.Id, out var assignment)
                    ? vehicles.GetValueOrDefault(assignment.VehicleId)
                    : null))
            .OrderBy(driver => driver.LastName)
            .ThenBy(driver => driver.FirstName)
            .ToList();
    }

    public async Task<DriverPageDto> SearchAsync(DriverSearchRequest request)
    {
        var result = await _driverRepository.SearchAsync(new DriverSearchQuery(
            request.Search,
            request.IsActive,
            request.HasVehicleAssignment,
            request.Page,
            request.PageSize));

        return new DriverPageDto
        {
            Items = result.Items.Select(Map).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalDriversCount = result.TotalDriversCount,
            ActiveCount = result.ActiveCount,
            WithVehicleCount = result.WithVehicleCount,
            InactiveCount = result.InactiveCount,
        };
    }

    public async Task<DriverDto?> GetByIdAsync(int id)
    {
        var driver = await _driverRepository.GetByIdAsync(id);
        if (driver == null) return null;

        return await MapAsync(driver);
    }

    public async Task<IReadOnlyList<DriverUserOptionDto>> GetAssignableUsersAsync()
    {
        var driverUsers = await _userRepository.GetByRolesAsync(new[] { UserRoles.Driver });
        var assignedUserIds = (await _driverRepository.GetAllAsync())
            .Select(driver => driver.UserId)
            .ToHashSet();

        return driverUsers
            .Where(user => !assignedUserIds.Contains(user.Id))
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new DriverUserOptionDto
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
            })
            .ToList();
    }

    public async Task<DriverDto> CreateAsync(CreateDriverRequest request)
    {
        var user = await RequireDriverUserAsync(request.UserId);
        if (await _driverRepository.GetByUserIdAsync(request.UserId) != null)
        {
            throw new InvalidOperationException("Wybrany użytkownik ma już profil kierowcy.");
        }

        var licenseNumber = RequireLicenseNumber(request.LicenseNumber);
        if (await _driverRepository.GetByLicenseNumberAsync(licenseNumber) != null)
        {
            throw new InvalidOperationException("Kierowca z tym numerem prawa jazdy już istnieje.");
        }

        var id = await _driverRepository.InsertAsync(new Driver
        {
            UserId = request.UserId,
            LicenseNumber = licenseNumber,
            IsActive = request.IsActive,
        });

        var created = await _driverRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Nie udało się odczytać utworzonego profilu kierowcy.");
        return Map(created, user);
    }

    public async Task<DriverDto?> UpdateAsync(int id, UpdateDriverRequest request)
    {
        var driver = await _driverRepository.GetByIdAsync(id);
        if (driver == null) return null;

        var licenseNumber = RequireLicenseNumber(request.LicenseNumber);
        var existing = await _driverRepository.GetByLicenseNumberAsync(licenseNumber);
        if (existing != null && existing.Id != id)
        {
            throw new InvalidOperationException("Kierowca z tym numerem prawa jazdy już istnieje.");
        }

        driver.LicenseNumber = licenseNumber;
        driver.IsActive = request.IsActive;
        await _driverRepository.UpdateAsync(driver);
        if (!driver.IsActive)
        {
            await _assignmentRepository.UnassignDriverAsync(driver.Id);
        }

        return await MapAsync(driver);
    }

    public async Task<DriverDto?> AssignVehicleAsync(int driverId, int vehicleId)
    {
        var driver = await _driverRepository.GetByIdAsync(driverId);
        if (driver == null) return null;
        if (!driver.IsActive)
        {
            throw new InvalidOperationException("Nie mozna przypisac pojazdu do nieaktywnego kierowcy.");
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
        if (vehicle == null || vehicle.Status != VehicleStatus.Active)
        {
            throw new InvalidOperationException("Wybrany pojazd nie istnieje albo nie jest aktywny.");
        }

        await _assignmentRepository.AssignAsync(driverId, vehicleId);
        return await MapAsync(driver);
    }

    public async Task<bool> UnassignVehicleAsync(int driverId)
    {
        if (await _driverRepository.GetByIdAsync(driverId) == null)
        {
            return false;
        }

        return await _assignmentRepository.UnassignDriverAsync(driverId);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await _assignmentRepository.UnassignDriverAsync(id);
        return await _driverRepository.DeleteAsync(id);
    }

    private async Task<User> RequireDriverUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRoles.Driver)
        {
            throw new InvalidOperationException("Profil kierowcy można przypisać tylko do użytkownika z rolą Driver.");
        }

        return user;
    }

    private static string RequireLicenseNumber(string licenseNumber)
    {
        var normalized = licenseNumber.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Podaj numer prawa jazdy.");
        }

        return normalized;
    }

    private async Task<DriverDto> MapAsync(Driver driver)
    {
        var assignment = await _assignmentRepository.GetActiveByDriverIdAsync(driver.Id);
        return Map(
            driver,
            await _userRepository.GetByIdAsync(driver.UserId),
            assignment,
            assignment == null ? null : await _vehicleRepository.GetByIdAsync(assignment.VehicleId));
    }

    private static DriverDto Map(
        Driver driver,
        User? user,
        DriverVehicleAssignment? assignment = null,
        Vehicle? vehicle = null)
    {
        return new DriverDto
        {
            Id = driver.Id,
            UserId = driver.UserId,
            FirstName = user?.FirstName ?? string.Empty,
            LastName = user?.LastName ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            LicenseNumber = driver.LicenseNumber,
            IsActive = driver.IsActive,
            CurrentVehicleId = assignment?.VehicleId,
            CurrentVehicleRegistration = vehicle?.RegistrationNumber,
            CurrentVehicleModel = vehicle?.Model,
        };
    }

    private static DriverDto Map(DriverListRow row)
    {
        return new DriverDto
        {
            Id = row.Id,
            UserId = row.UserId,
            FirstName = row.FirstName,
            LastName = row.LastName,
            Email = row.Email,
            LicenseNumber = row.LicenseNumber,
            IsActive = row.IsActive,
            CurrentVehicleId = row.CurrentVehicleId,
            CurrentVehicleRegistration = row.CurrentVehicleRegistration,
            CurrentVehicleModel = row.CurrentVehicleModel,
        };
    }
}
