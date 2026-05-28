using System.Security.Claims;
using KuchniaUCygana.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace KuchniaUCygana.Infrastructure.Auth;

/// <summary>
/// Implementacja ICurrentUserService oparta o IHttpContextAccessor.
/// Pobiera dane zalogowanego użytkownika z claims ASP.NET Core Identity.
/// Rejestrowana jako Scoped (per-request).
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Parsuje NameIdentifier claim jako int.
    /// Zwraca null jeśli brak sesji lub claim nie jest liczbą.
    /// </summary>
    public int? GetUserId()
    {
        var claim = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var id))
            return id;
        return null;
    }

    /// <summary>
    /// Zwraca claim Name (username) lub null jeśli brak sesji.
    /// </summary>
    public string? GetUserName()
    {
        return User?.FindFirstValue(ClaimTypes.Name);
    }
}
