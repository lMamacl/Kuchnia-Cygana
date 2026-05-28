using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;

namespace KuchniaUCygana.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IWebHostEnvironment env;

    public AccountController(IWebHostEnvironment env)
    {
        this.env = env;
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

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, $"dev-{role.ToLower()}@kuchniaucygana.pl"),
            new Claim(ClaimTypes.Role, role)
        };

        // Manager automatycznie dostaje też claim bazowego pracownika
        if (role.EndsWith("Manager", StringComparison.Ordinal))
        {
            var baseRole = role.Replace("Manager", string.Empty);
            claims.Add(new Claim(ClaimTypes.Role, baseRole));
        }

        // Admin dostaje dostęp do wszystkiego — dodajemy wszystkie role
        if (role == "Admin")
        {
            foreach (var r in new[] { "Kitchen", "KitchenManager", "Warehouse", "WarehouseManager", "Packing", "PackingManager", "Dietitian", "Driver" })
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
            if (role.Contains("Kitchen")) return RedirectToAction("Index", "Production");
            if (role.Contains("Warehouse")) return RedirectToAction("Index", "Warehouse");
            if (role.Contains("Packing")) return RedirectToAction("Index", "Packing");
            if (role.Contains("Dietitian")) return RedirectToAction("Index", "DietEditor");
            if (role.Contains("Driver")) return RedirectToAction("Index", "DriverMobile");
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
