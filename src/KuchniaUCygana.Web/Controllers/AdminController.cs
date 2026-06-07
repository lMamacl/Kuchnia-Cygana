using System.Security.Claims;
using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Constants;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController : Controller
{
    private static readonly IReadOnlyList<string> AvailableRoles = new[] { UserRoles.Client }
        .Concat(AppRoles.StaffRoleNames)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private readonly IAuditLogService auditLogService;
    private readonly IStaffActivityService staffActivityService;
    private readonly IUserRepository userRepository;
    private readonly IUserService userService;

    public AdminController(
        IAuditLogService auditLogService,
        IStaffActivityService staffActivityService,
        IUserRepository userRepository,
        IUserService userService)
    {
        this.auditLogService = auditLogService;
        this.staffActivityService = staffActivityService;
        this.userRepository = userRepository;
        this.userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Admin";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Dashboard administracyjny systemu.";
        return View(await BuildModelAsync());
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(int page = 1, int pageSize = 10)
    {
        ViewData["Title"] = "Uzytkownicy";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie uzytkownikami.";
        return View(await BuildModelAsync(usersPage: page, usersPageSize: pageSize));
    }

    [HttpGet("users/new")]
    public async Task<IActionResult> NewUser()
    {
        ViewData["Title"] = "Nowy uzytkownik";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Tworzenie konta uzytkownika.";
        return View(await BuildModelAsync());
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles()
    {
        ViewData["Title"] = "Role";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Zarzadzanie rolami i uprawnieniami.";
        return View(await BuildModelAsync());
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] AuditLogFilterViewModel filter)
    {
        ViewData["Title"] = "Logi systemowe";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Audyt zmian w systemie.";
        return View(await BuildModelAsync(filter));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        ViewData["Title"] = "Ustawienia";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Ustawienia systemowe.";
        return View(await BuildModelAsync());
    }

    [HttpPost("users")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateAdminUserViewModel request)
    {
        var email = Normalize(request.Email);
        var firstName = Normalize(request.FirstName);
        var lastName = Normalize(request.LastName);
        var role = Normalize(request.Role);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            TempData["Error"] = "Podaj email oraz haslo dluzsze niz 5 znakow.";
            SetNewUserViewData();
            return View("NewUser", await BuildModelAsync(newUser: request));
        }

        if (!IsAllowedRole(role))
        {
            TempData["Error"] = "Wybrana rola nie jest dostepna.";
            SetNewUserViewData();
            return View("NewUser", await BuildModelAsync(newUser: request));
        }

        if (await userRepository.FindByEmailAsync(email) is not null)
        {
            TempData["Error"] = "Konto z takim adresem email juz istnieje.";
            SetNewUserViewData();
            return View("NewUser", await BuildModelAsync(newUser: request));
        }

        var user = new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        var userId = await userRepository.InsertAsync(user);
        await staffActivityService.RecordAsync(
            "Admin.CreateUser",
            "User",
            userId.ToString(),
            newValue: new
            {
                userId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
            },
            notification: BuildNotification(
                "Admin",
                NotificationSeverity.Info,
                "Utworzono konto",
                $"Dodano konto {user.Email} z rola {user.Role}.",
                "/admin/users",
                "User",
                userId),
            notifyRoles: AdminNotificationRoles);
        TempData["Success"] = "Uzytkownik zostal utworzony.";

        return RedirectToAction(nameof(Users));
    }

    [HttpPost("users/{id:int}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(int id, UpdateAdminUserViewModel request)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "Nie znaleziono uzytkownika.";
            return RedirectToAction(nameof(Users));
        }

        var email = Normalize(request.Email);
        var role = Normalize(request.Role);

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Email nie moze byc pusty.";
            return RedirectToAction(nameof(Users));
        }

        if (!IsAllowedRole(role))
        {
            TempData["Error"] = "Wybrana rola nie jest dostepna.";
            return RedirectToAction(nameof(Users));
        }

        var emailOwner = await userRepository.FindByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != id)
        {
            TempData["Error"] = "Adres email jest juz przypisany do innego konta.";
            return RedirectToAction(nameof(Users));
        }

        if (user.Role == AppRoles.Admin && role != AppRoles.Admin && await CountAdminsAsync() <= 1)
        {
            TempData["Error"] = "Nie mozna odebrac roli ostatniemu administratorowi.";
            return RedirectToAction(nameof(Users));
        }

        var oldValue = new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
        };

        user.Email = email;
        user.FirstName = Normalize(request.FirstName);
        user.LastName = Normalize(request.LastName);
        user.Role = role;

        await userRepository.UpdateAsync(user);
        await staffActivityService.RecordAsync(
            "Admin.UpdateUser",
            "User",
            user.Id.ToString(),
            oldValue: oldValue,
            newValue: new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
            },
            notification: BuildNotification(
                "Admin",
                NotificationSeverity.Info,
                "Zaktualizowano konto",
                $"Zapisano dane konta {user.Email}.",
                "/admin/users",
                "User",
                user.Id),
            notifyRoles: AdminNotificationRoles);
        TempData["Success"] = "Dane uzytkownika zostaly zapisane.";

        return RedirectToAction(nameof(Users));
    }

    [HttpPost("users/{id:int}/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(int id, ResetAdminUserPasswordViewModel request)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "Nie znaleziono uzytkownika.";
            return RedirectToAction(nameof(Users));
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            TempData["Error"] = "Nowe haslo musi miec co najmniej 6 znakow.";
            return RedirectToAction(nameof(Users));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await userRepository.UpdateAsync(user);
        await staffActivityService.RecordAsync(
            "Admin.ResetUserPassword",
            "User",
            user.Id.ToString(),
            newValue: new
            {
                user.Id,
                user.Email,
                PasswordChanged = true,
            },
            notification: BuildNotification(
                "Admin",
                NotificationSeverity.Warning,
                "Zresetowano haslo",
                $"Haslo konta {user.Email} zostalo zresetowane.",
                "/admin/users",
                "User",
                user.Id),
            notifyRoles: AdminNotificationRoles);

        TempData["Success"] = "Haslo uzytkownika zostalo zresetowane.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("users/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (id == GetCurrentUserId())
        {
            TempData["Error"] = "Nie mozna usunac aktualnie zalogowanego konta.";
            return RedirectToAction(nameof(Users));
        }

        var user = await userRepository.GetByIdAsync(id);
        if (user is null)
        {
            TempData["Error"] = "Nie znaleziono uzytkownika.";
            return RedirectToAction(nameof(Users));
        }

        if (user.Role == AppRoles.Admin && await CountAdminsAsync() <= 1)
        {
            TempData["Error"] = "Nie mozna usunac ostatniego administratora.";
            return RedirectToAction(nameof(Users));
        }

        try
        {
            var deleted = await userRepository.DeleteAsync(id);
            if (deleted)
            {
                await staffActivityService.RecordAsync(
                    "Admin.DeleteUser",
                    "User",
                    id.ToString(),
                    oldValue: new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        user.Role,
                    },
                    notification: BuildNotification(
                        "Admin",
                        NotificationSeverity.Warning,
                        "Usunieto konto",
                        $"Konto {user.Email} zostalo usuniete.",
                        "/admin/users",
                        "User",
                        id),
                    notifyRoles: AdminNotificationRoles);
            }

            TempData[deleted ? "Success" : "Error"] = deleted
                ? "Uzytkownik zostal usuniety."
                : "Uzytkownik nie zostal usuniety.";
        }
        catch
        {
            TempData["Error"] = "Nie mozna usunac uzytkownika powiazanego z innymi danymi. Zmien role albo dezaktywuj konto w module HR.";
        }

        return RedirectToAction(nameof(Users));
    }

    private async Task<AdminDashboardViewModel> BuildModelAsync(
        AuditLogFilterViewModel? filter = null,
        CreateAdminUserViewModel? newUser = null,
        int usersPage = 1,
        int usersPageSize = 10)
    {
        var auditFilter = filter ?? new AuditLogFilterViewModel { Page = 1, PageSize = 10 };
        auditFilter.From ??= DateTime.Today.AddDays(-30);
        var auditPage = await auditLogService.SearchSystemLogsAsync(auditFilter.ToSearchRequest());
        var users = (await userService.GetAllAsync())
            .OrderBy(user => user.Role)
            .ThenBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .ToArray();

        return new AdminDashboardViewModel
        {
            SystemLogs = auditPage.Items,
            Users = users,
            AvailableRoles = AvailableRoles,
            UsersPage = PagedList<UserDto>.Create(users, usersPage, usersPageSize),
            AuditPage = auditPage,
            AuditFilter = auditFilter,
            NewUser = newUser ?? new CreateAdminUserViewModel(),
        };
    }

    private void SetNewUserViewData()
    {
        ViewData["Title"] = "Nowy uzytkownik";
        ViewData["Section"] = "Administracja";
        ViewData["Description"] = "Tworzenie konta uzytkownika.";
    }

    private static bool IsAllowedRole(string role)
    {
        return AvailableRoles.Contains(role, StringComparer.Ordinal);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private async Task<int> CountAdminsAsync()
    {
        var users = await userRepository.GetByRolesAsync([AppRoles.Admin]);
        return users.Count;
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static readonly string[] AdminNotificationRoles = [AppRoles.Admin];

    private static Notification BuildNotification(
        string type,
        string severity,
        string title,
        string message,
        string linkUrl,
        string sourceType,
        long sourceId)
        => new()
        {
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            SourceType = sourceType,
            SourceId = sourceId,
        };
}
