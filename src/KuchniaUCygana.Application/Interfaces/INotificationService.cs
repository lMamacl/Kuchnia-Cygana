using KuchniaUCygana.Application.DTOs.Notifications;
using KuchniaUCygana.Domain.Entities.Notifications;

namespace KuchniaUCygana.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationSummaryDto> GetCurrentUserSummaryAsync(int limit = 5);

    Task<PagedNotificationListDto> GetCurrentUserNotificationsAsync(NotificationListFilterDto filter);

    Task CreateForRolesAsync(Notification notification, IEnumerable<string> roles);

    Task<bool> MarkAsReadAsync(long userNotificationId);

    Task<int> MarkManyAsReadAsync(IEnumerable<long> userNotificationIds);

    Task<int> MarkAllAsReadAsync();

    Task<bool> ArchiveAsync(long userNotificationId);

    Task<int> ArchiveManyAsync(IEnumerable<long> userNotificationIds);

    Task<int> ArchiveReadAsync();
}
