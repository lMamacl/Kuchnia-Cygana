using System.Security.Claims;
using KuchniaUCygana.Domain.Constants;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

public sealed class AccountController : Controller
{
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
        ViewData["Description"] = "Szkielet centrum konta klienta.";
        return View();
    }

    [HttpGet]
    public IActionResult Profile()
    {
        ViewData["Title"] = "Profil klienta";
        ViewData["Description"] = "Placeholder profilu klienta.";
        return View();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet]
    [Route("staff/login")]
    public IActionResult StaffLogin(string? returnUrl = null)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl ?? Url.Action("Index", "Staff"),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Logowanie jest pominiete w wersji preview.";
        return Redirect(model.ReturnUrl ?? Url.Action(nameof(Index), "Account") ?? "/account");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Rejestracja jest pominieta w wersji preview.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("account/dev-login")]
    public async Task<IActionResult> DevLogin(string role, string? returnUrl = null)
    {
        if (!env.IsDevelopment())
        {
            return BadRequest("Logowanie deweloperskie jest wyłączone na tym środowisku.");
        }

        if (string.IsNullOrWhiteSpace(role) || !AppRoles.IsStaffRole(role))
        {
            return BadRequest("Nieznana rola pracownicza.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, $"dev-{role.ToLowerInvariant()}@kuchniaucygana.pl"),
            new Claim(ClaimTypes.Role, role)
        };

        var devUser = await userRepository.GetFirstByRoleAsync(role);
        if (devUser is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, devUser.Id.ToString()));
            claims.RemoveAll(claim => claim.Type == ClaimTypes.Name);
            claims.Add(new Claim(ClaimTypes.Name, devUser.Email));
        }

        // Manager automatycznie dostaje też claim bazowego pracownika
        if (role.EndsWith("Manager", StringComparison.Ordinal))
        {
            var baseRole = role.Replace("Manager", string.Empty);
            claims.Add(new Claim(ClaimTypes.Role, baseRole));
        }

        // Admin dostaje dostęp do wszystkiego — dodajemy wszystkie role
        if (role == AppRoles.Admin)
        {
            foreach (var r in AppRoles.StaffRoleNames)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        TempData["Success"] = $"Zalogowano jako: {role} (Bypass HR).";

        if (string.IsNullOrEmpty(returnUrl))
        {
            if (role.Contains(AppRoles.Kitchen, StringComparison.Ordinal)) return RedirectToAction("Index", "Production");
            if (role.Contains(AppRoles.Warehouse, StringComparison.Ordinal)) return RedirectToAction("Index", "Warehouse");
            if (role.Contains(AppRoles.Packing, StringComparison.Ordinal)) return RedirectToAction("Index", "Packing");
            if (role.Contains(AppRoles.Dietitian, StringComparison.Ordinal)) return RedirectToAction("Index", "DietEditor");
            if (role.Contains(AppRoles.Driver, StringComparison.Ordinal)) return RedirectToAction("Index", "DriverMobile");
            if (role.Contains(AppRoles.Logistics, StringComparison.Ordinal)) return RedirectToAction("Index", "Logistics");
            return RedirectToAction("Index", "Staff");
        }

        return Redirect(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Pomyślnie wylogowano.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Brak dostepu";
        ViewData["Description"] = "Preview nie wymusza uprawnien. Ten widok zostaje jako przyszly punkt integracji.";
        return View();
    }
}
