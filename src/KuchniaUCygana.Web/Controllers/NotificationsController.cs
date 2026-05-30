using KuchniaUCygana.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize]
[Route("notifications")]
public sealed class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("{userNotificationId:long}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(long userNotificationId, string? returnUrl = null)
    {
        await _notificationService.MarkAsReadAsync(userNotificationId);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost("read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(string? returnUrl = null)
    {
        await _notificationService.MarkAllAsReadAsync();
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Staff") ?? "/";
    }
}
