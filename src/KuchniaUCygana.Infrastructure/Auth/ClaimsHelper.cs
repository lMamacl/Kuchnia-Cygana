using KuchniaUCygana.Domain.Enums;
using System.Security.Claims;

namespace KuchniaUCygana.Infrastructure.Auth;

public static class ClaimsHelper
{
    public static int GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException();
        return int.Parse(value);
    }

    public static bool IsAdmin(ClaimsPrincipal principal)
    {
        return principal.IsInRole(UserRoles.Admin);
    }
}
