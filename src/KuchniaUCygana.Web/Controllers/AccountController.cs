using System.Security.Claims;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace KuchniaUCygana.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly ICustomerProfileService customerProfileService;
    private readonly IWebHostEnvironment env;

    public AccountController(
        IUserRepository userRepository,
        ICustomerProfileService customerProfileService,
        IWebHostEnvironment env)
    {
        this.userRepository = userRepository;
        this.customerProfileService = customerProfileService;
        this.env = env;
    }

    [HttpGet]
    [Authorize]
    public IActionResult Index()
    {
        ViewData["Title"] = "Konto";
        ViewData["Description"] = "Szkielet centrum konta klienta.";
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult Profile()
    {
        ViewData["Title"] = "Profil klienta";
        ViewData["Description"] = "Placeholder profilu klienta.";
        return View();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userRepository.FindByEmailAsync(model.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Nieprawidłowy adres e-mail lub hasło.");
            return View(model);
        }

        await SignInUserAsync(user);
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Order");

        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await userRepository.ExistsWithEmailAsync(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Konto z tym adresem e-mail już istnieje.");
            return View(model);
        }

        var user = new User
        {
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            FirstName = model.FirstName,
            LastName = model.LastName,
            Role = UserRoles.Client,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var userId = await userRepository.InsertAsync(user);
        user.Id = userId;

        await customerProfileService.EnsureProfileExistsAsync(userId);
        await SignInUserAsync(user);

        TempData["Success"] = "Konto zostało pomyślnie utworzone!";
        return RedirectToAction("Index", "Home");
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
            new Claim(ClaimTypes.NameIdentifier, "0"),
            new Claim(ClaimTypes.Name, $"dev-{role.ToLower()}@kuchniaucygana.pl"),
            new Claim(ClaimTypes.Email, $"dev-{role.ToLower()}@kuchniaucygana.pl"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        TempData["Success"] = $"Zalogowano jako: {role} (Bypass HR).";

        if (string.IsNullOrEmpty(returnUrl))
        {
            if (role.Contains("Kitchen")) return RedirectToAction("Index", "Production");
            if (role.Contains("Warehouse")) return RedirectToAction("Index", "Warehouse");
            if (role.Contains("Packing")) return RedirectToAction("Index", "Packing");
            return RedirectToAction("Index", "Staff");
        }

        return Redirect(returnUrl);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();

        TempData["Success"] = "Pomyślnie wylogowano.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Brak dostępu";
        ViewData["Description"] = "Ten widok służy jako punkt integracji zabezpieczeń ról.";
        return View();
    }

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Role, user.Role),
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Order");
    }
}
