using System.Security.Claims;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KuchniaUCygana.Web.Filters;

public sealed class StaffShiftGuardFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
    };

    private static readonly HashSet<string> StaffRoles = new(AppRoles.StaffRoleNames, StringComparer.Ordinal);

    private readonly IStaffShiftAccessService staffShiftAccessService;

    public StaffShiftGuardFilter(IStaffShiftAccessService staffShiftAccessService)
    {
        this.staffShiftAccessService = staffShiftAccessService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ShouldGuard(context))
        {
            await next();
            return;
        }

        if (await staffShiftAccessService.IsCurrentUserInsideActiveShiftAsync(DateTimeOffset.Now))
        {
            await next();
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData["Error"] = "Zmiany w modulach firmowych mozesz wprowadzac tylko podczas swojej aktywnej zmiany.";
        }

        context.Result = BuildBlockedResult(context);
    }

    private static bool ShouldGuard(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (!MutatingMethods.Contains(request.Method))
        {
            return false;
        }

        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<AllowAnonymousAttribute>().Any() ||
            metadata.OfType<AllowOutsideShiftAttribute>().Any())
        {
            return false;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!IsStaffEndpoint(metadata) || !IsOrdinaryStaffUser(user))
        {
            return false;
        }

        return true;
    }

    private static bool IsStaffEndpoint(IEnumerable<object> metadata)
    {
        return metadata
            .OfType<AuthorizeAttribute>()
            .SelectMany(attribute => SplitRoles(attribute.Roles))
            .Any(role => StaffRoles.Contains(role));
    }

    private static bool IsOrdinaryStaffUser(ClaimsPrincipal user)
    {
        var roles = user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToArray();

        if (roles.Any(role =>
                string.Equals(role, AppRoles.Admin, StringComparison.Ordinal) ||
                role.EndsWith("Manager", StringComparison.Ordinal)))
        {
            return false;
        }

        return roles.Any(role => StaffRoles.Contains(role));
    }

    private static IEnumerable<string> SplitRoles(string? roles)
    {
        return string.IsNullOrWhiteSpace(roles)
            ? Array.Empty<string>()
            : roles
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(role => !string.IsNullOrWhiteSpace(role));
    }

    private static IActionResult BuildBlockedResult(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.Path.StartsWithSegments("/api") ||
            string.Equals(request.Headers.XRequestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new
            {
                message = "Zmiany mozna wprowadzac tylko podczas aktywnej zmiany.",
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }

        var referer = request.Headers.Referer.ToString();
        if (TryBuildLocalRedirect(request, referer, out var localRedirect))
        {
            return new RedirectResult(localRedirect);
        }

        return new RedirectToActionResult("Index", "Staff", null);
    }

    private static bool TryBuildLocalRedirect(HttpRequest request, string? referer, out string localRedirect)
    {
        localRedirect = string.Empty;
        if (string.IsNullOrWhiteSpace(referer))
        {
            return false;
        }

        if (Uri.TryCreate(referer, UriKind.Relative, out _) && referer.StartsWith('/'))
        {
            localRedirect = referer;
            return true;
        }

        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var requestHost = request.Host.Value;
        var refererHost = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        if (!string.Equals(requestHost, refererHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        localRedirect = uri.PathAndQuery;
        return true;
    }
}
