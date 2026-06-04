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

    /// <summary>
    /// Initializes a new instance of <see cref="AccountController"/> and captures the hosting environment for environment-specific behavior.
    /// </summary>
    public AccountController(IWebHostEnvironment env)
    {
        this.env = env;
    }
    /// <summary>
    /// Displays the account center main view and prepares view metadata.
    /// </summary>
    /// <returns>The view for the account index page.</returns>
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Konto";
        ViewData["Description"] = "Szkielet centrum konta klienta.";
        return View();
    }

    /// <summary>
    /// Displays the client profile view and prepares page metadata.
    /// </summary>
    /// <returns>The profile view result. ViewData["Title"] is set to "Profil klienta" and ViewData["Description"] to "Placeholder profilu klienta."</returns>
    [HttpGet]
    public IActionResult Profile()
    {
        ViewData["Title"] = "Profil klienta";
        ViewData["Description"] = "Placeholder profilu klienta.";
        return View();
    }

    /// <summary>
    /// Displays the login page.
    /// </summary>
    /// <param name="returnUrl">Optional URL to redirect to after successful login; preserved in the view model.</param>
    /// <returns>The login view populated with a <c>LoginViewModel</c> whose <c>ReturnUrl</c> is set to the provided value.</returns>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// Handles submitted login data in the preview environment and redirects the user without performing authentication.
    /// </summary>
    /// <param name="model">The submitted login view model; its <c>ReturnUrl</c> determines the post-login redirect when present.</param>
    /// <returns>A redirect to <c>model.ReturnUrl</c> if provided; otherwise to the Account controller's Index action, falling back to <c>/account</c>.</returns>
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Logowanie jest pominiete w wersji preview.";
        return Redirect(model.ReturnUrl ?? Url.Action(nameof(Index), "Account") ?? "/account");
    }

    /// <summary>
    /// Displays the registration page with a new, empty registration model.
    /// </summary>
    /// <returns>The registration view populated with a new <see cref="RegisterViewModel"/>.</returns>
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    /// <summary>
    /// Processes registration submissions in preview mode without creating a user account.
    /// </summary>
    /// <param name="model">The submitted registration form values.</param>
    /// <returns>A redirect to the account index action.</returns>
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        await Task.CompletedTask;
        TempData["Success"] = "Rejestracja jest pominieta w wersji preview.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Performs a development-only sign-in that creates a cookie-authenticated user with the specified role and redirects to an appropriate page.
    /// </summary>
    /// <param name="role">The role name to assign to the created user; used for the generated user email and role claim.</param>
    /// <param name="returnUrl">Optional URL to redirect to after sign-in; when null or empty the action redirects based on the provided role.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> that is:
    /// - a BadRequest result with an explanatory message when the host environment is not development;
    /// - a redirect to <paramref name="returnUrl"/> when provided;
    /// - otherwise a redirect to a role-specific controller index action.
    /// </returns>
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

    /// <summary>
    /// Signs the current user out of the cookie authentication scheme and redirects to the home page.
    /// </summary>
    /// <remarks>Sets <c>TempData["Success"]</c> to a confirmation message indicating successful logout.</remarks>
    /// <returns>A redirect to the Home controller's Index action.</returns>
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Pomyślnie wylogowano.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Displays the access-denied page used as a placeholder for future permission enforcement.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that renders the access denied view.</returns>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Brak dostepu";
        ViewData["Description"] = "Preview nie wymusza uprawnien. Ten widok zostaje jako przyszly punkt integracji.";
        return View();
    }
}
