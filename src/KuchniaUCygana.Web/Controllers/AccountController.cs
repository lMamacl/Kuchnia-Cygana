using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

public sealed class AccountController : Controller
{
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
    public async Task<IActionResult> Logout()
    {
        await Task.CompletedTask;
        TempData["Success"] = "Wylogowanie jest pominiete w wersji preview.";
        return RedirectToAction(nameof(Index), "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Brak dostepu";
        ViewData["Description"] = "Preview nie wymusza uprawnien. Ten widok zostaje jako przyszly punkt integracji.";
        return View();
    }
}
