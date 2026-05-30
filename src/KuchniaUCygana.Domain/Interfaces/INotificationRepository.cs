using KuchniaUCygana.Domain.Entities.Notifications;

namespace KuchniaUCygana.Domain.Interfaces;

public interface INotificationRepository : IRepository<Notification, long>
{
    Task<long> CreateForUsersAsync(Notification notification, IReadOnlyCollection<int> userIds);

    Task<(IReadOnlyList<UserNotificationRow> Items, int UnreadCount)> GetForUserAsync(int userId, int limit);

    Task<bool> MarkAsReadAsync(long userNotificationId, int userId);

    Task<int> MarkAllAsReadAsync(int userId);
}

public sealed class UserNotificationRow
{
    public long UserNotificationId { get; set; }

    public long NotificationId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset DeliveredAt { get; set; }
}
