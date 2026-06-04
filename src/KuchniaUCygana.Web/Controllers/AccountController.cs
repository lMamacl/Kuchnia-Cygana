using System.Security.Claims;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

public sealed class AccountController : Controller
{
    private static readonly string[] StaffRoles =
    [
        UserRoles.Kitchen,
        UserRoles.KitchenManager,
        UserRoles.Warehouse,
        UserRoles.WarehouseManager,
        UserRoles.Packing,
        UserRoles.PackingManager,
        UserRoles.Dietitian,
        UserRoles.Logistics,
        UserRoles.LogisticsManager,
        UserRoles.Driver,
        UserRoles.DriverManager,
        UserRoles.HR,
        UserRoles.HRManager,
        UserRoles.BOK,
        UserRoles.BOKManager,
    ];

    private readonly IWebHostEnvironment env;
    private readonly IUserRepository userRepository;

    public AccountController(IWebHostEnvironment env, IUserRepository userRepository)
    {
        this.env = env;
        this.userRepository = userRepository;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Konto";
        ViewData["Description"] = "Centrum konta uzytkownika.";
        return View();
    }

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        ViewData["Title"] = "Profil";
        ViewData["Description"] = "Profil zalogowanego uzytkownika.";
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome(GetPrimaryRole(User), returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        model.ReturnUrl ??= returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var user = await userRepository.FindByEmailAsync(normalizedEmail);
        if (user is null || !VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Nieprawidlowy email lub haslo.");
            return View(model);
        }

        await SignInAsync(
            user.Id.ToString(),
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Email,
            user.Role);

        TempData["Success"] = $"Zalogowano jako {user.Role}.";
        return RedirectToRoleHome(user.Role, model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Rejestracja jest pominieta w wersji preview.";
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("account/dev-login")]
    public async Task<IActionResult> DevLogin(string role, string? returnUrl = null)
    {
        if (!env.IsDevelopment())
        {
            return BadRequest("Logowanie deweloperskie jest wylaczone na tym srodowisku.");
        }

        if (!StaffRoles.Contains(role, StringComparer.Ordinal) && role != UserRoles.Admin)
        {
            return BadRequest("Nieznana rola deweloperska.");
        }

        await SignInAsync(
            "0",
            $"Dev {role}",
            $"dev-{role.ToLowerInvariant()}@kuchniaucygana.pl",
            role);

        TempData["Success"] = $"Zalogowano jako: {role} (dev).";
        return RedirectToRoleHome(role, returnUrl);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Pomyslnie wylogowano.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Brak dostepu";
        ViewData["Description"] = "Nie masz uprawnien do wybranego widoku.";
        return View();
    }

    private async Task SignInAsync(string userId, string displayName, string email, string role)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? email : displayName;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
        };

        foreach (var expandedRole in ExpandRoles(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, expandedRole));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    private IActionResult RedirectToRoleHome(string? role, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return role switch
        {
            UserRoles.Admin => RedirectToAction("Index", "Staff"),
            UserRoles.HR or UserRoles.HRManager => RedirectToAction("Index", "HumanResources"),
            UserRoles.BOK or UserRoles.BOKManager => RedirectToAction("Index", "CustomerSupport"),
            UserRoles.Kitchen or UserRoles.KitchenManager => RedirectToAction("Index", "Production"),
            UserRoles.Warehouse or UserRoles.WarehouseManager => RedirectToAction("Index", "Warehouse"),
            UserRoles.Packing or UserRoles.PackingManager => RedirectToAction("Index", "Packing"),
            UserRoles.Dietitian => RedirectToAction("Index", "DietEditor"),
            UserRoles.Logistics or UserRoles.LogisticsManager => RedirectToAction("Index", "Logistics"),
            UserRoles.Driver or UserRoles.DriverManager => RedirectToAction("Index", "DriverMobile"),
            UserRoles.Client => RedirectToAction("Index", "Account"),
            _ => RedirectToAction("Index", "Home"),
        };
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IEnumerable<string> ExpandRoles(string role)
    {
        var roles = new HashSet<string>(StringComparer.Ordinal) { role };

        if (role == UserRoles.Admin)
        {
            foreach (var staffRole in StaffRoles)
            {
                roles.Add(staffRole);
            }
        }
        else if (role.EndsWith("Manager", StringComparison.Ordinal))
        {
            roles.Add(role[..^"Manager".Length]);
        }

        return roles;
    }

    private static string? GetPrimaryRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(UserRoles.Admin))
        {
            return UserRoles.Admin;
        }

        return StaffRoles.FirstOrDefault(user.IsInRole) ??
            user.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Role)?.Value;
    }
}
