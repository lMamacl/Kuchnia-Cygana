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
    private readonly IUserRepository _userRepository;

    public DriverService(IDriverRepository driverRepository, IUserRepository userRepository)
    {
        _driverRepository = driverRepository;
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<DriverDto>> GetAllAsync()
    {
        var drivers = await _driverRepository.GetAllAsync();
        var users = (await _userRepository.GetAllAsync()).ToDictionary(user => user.Id);

        return drivers
            .Select(driver => Map(driver, users.GetValueOrDefault(driver.UserId)))
            .OrderBy(driver => driver.LastName)
            .ThenBy(driver => driver.FirstName)
            .ToList();
    }

    public async Task<DriverDto?> GetByIdAsync(int id)
    {
        var driver = await _driverRepository.GetByIdAsync(id);
        if (driver == null) return null;

        return Map(driver, await _userRepository.GetByIdAsync(driver.UserId));
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

        return Map(driver, await _userRepository.GetByIdAsync(driver.UserId));
    }

    public Task<bool> DeleteAsync(int id)
    {
        return _driverRepository.DeleteAsync(id);
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

    private static DriverDto Map(Driver driver, User? user)
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
        };
    }
}
