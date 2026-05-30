using KuchniaUCygana.Application.DTOs.Notifications;
using KuchniaUCygana.Domain.Entities.Notifications;

namespace KuchniaUCygana.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationSummaryDto> GetCurrentUserSummaryAsync(int limit = 5);

    Task CreateForRolesAsync(Notification notification, IEnumerable<string> roles);

    Task<bool> MarkAsReadAsync(long userNotificationId);

    Task<int> MarkAllAsReadAsync();
}
