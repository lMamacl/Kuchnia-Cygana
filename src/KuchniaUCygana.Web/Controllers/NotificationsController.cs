using KuchniaUCygana.Application.DTOs.Notifications;
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

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] NotificationListFilterDto filter)
    {
        var model = await _notificationService.GetCurrentUserNotificationsAsync(filter);
        return View(model);
    }

    [HttpPost("{userNotificationId:long}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(long userNotificationId, string? returnUrl = null)
    {
        await _notificationService.MarkAsReadAsync(userNotificationId);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost("{userNotificationId:long}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(long userNotificationId, string? returnUrl = null)
    {
        await _notificationService.ArchiveAsync(userNotificationId);
        TempData["Success"] = "Powiadomienie usunięte z Twojej listy.";
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost("read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(string? returnUrl = null)
    {
        await _notificationService.MarkAllAsReadAsync();
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost("archive-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveRead(string? returnUrl = null)
    {
        var count = await _notificationService.ArchiveReadAsync();
        TempData["Success"] = count > 0
            ? $"Usunięto {count} przeczytanych powiadomień z Twojej listy."
            : "Nie ma przeczytanych powiadomień do usunięcia.";
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost("bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bulk(NotificationBulkActionRequest request, string? returnUrl = null)
    {
        var ids = request.UserNotificationIds ?? new List<long>();
        if (!ids.Any())
        {
            TempData["Error"] = "Zaznacz co najmniej jedno powiadomienie.";
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        if (string.Equals(request.Action, "MarkRead", StringComparison.OrdinalIgnoreCase))
        {
            var count = await _notificationService.MarkManyAsReadAsync(ids);
            TempData["Success"] = $"Oznaczono jako przeczytane: {count}.";
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        if (string.Equals(request.Action, "Archive", StringComparison.OrdinalIgnoreCase))
        {
            var count = await _notificationService.ArchiveManyAsync(ids);
            TempData["Success"] = $"Usunięto z Twojej listy: {count}.";
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        TempData["Error"] = "Nieznana akcja masowa.";
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action(nameof(Index), "Notifications") ?? "/";
    }
}
